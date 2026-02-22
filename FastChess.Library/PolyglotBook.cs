using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Numerics;

namespace FastChess.Library
{
    public sealed class PolyglotBook
    {
        private const int EntrySize = 16;
#if !NET6_0_OR_GREATER
        [ThreadStatic] private static Random? _threadRandom;
        private static int _seed = Environment.TickCount;
#endif
        private static readonly CastlingRights[] CastleRightsOrder =
        {
            CastlingRights.WhiteKingSide,
            CastlingRights.WhiteQueenSide,
            CastlingRights.BlackKingSide,
            CastlingRights.BlackQueenSide
        };

        public string BookFile { get; init; }

        public PolyglotBook(string bookFile)
        {
            BookFile = bookFile ?? throw new ArgumentNullException(nameof(bookFile));
        }

        public Move Probe(IPosition pos, bool pickBest = true)
        {
            return TryProbe(pos, out var move, pickBest) ? move : default;
        }

        public bool TryProbe(IPosition pos, out Move move, bool pickBest = true)
        {
            if (pos == null) throw new ArgumentNullException(nameof(pos));
            if (string.IsNullOrWhiteSpace(BookFile)) throw new InvalidOperationException("BookFile is required.");

            var key = ComputePolyglotKey(in pos);
            var candidates = LoadCandidates(pos, key);
            if (candidates.Count == 0)
            {
                move = default;
                return false;
            }

            if (pickBest)
            {
                PolyglotEntry best = candidates[0];
                for (int i = 1; i < candidates.Count; i++)
                {
                    if (candidates[i].Weight > best.Weight)
                        best = candidates[i];
                }
                move = best.Move;
                return true;
            }

            int totalWeight = 0;
            for (int i = 0; i < candidates.Count; i++)
                totalWeight += candidates[i].Weight;

            if (totalWeight <= 0)
            {
                move = candidates[NextRandom(candidates.Count)].Move;
                return true;
            }

            int pick = NextRandom(totalWeight);
            int run = 0;
            for (int i = 0; i < candidates.Count; i++)
            {
                run += candidates[i].Weight;
                if (pick < run)
                {
                    move = candidates[i].Move;
                    return true;
                }
            }

            move = candidates[^1].Move;
            return true;
        }

        public HashKey ComputePolyglotKey(in IPosition pos)
        {
            var k = HashKey.Empty;
            ulong b = pos.Pieces();

            while (b != 0)
            {
                int sqIdx = BitOperations.TrailingZeroCount(b);
                b &= b - 1;

                var s = new Square((byte)sqIdx);
                var pc = pos.GetPiece(s);
                k ^= PolyglotBookZobrist.Psq(pc, s);
            }

            for (int i = 0; i < CastleRightsOrder.Length; i++)
            {
                CastlingRights right = CastleRightsOrder[i];
                if ((pos.CastleRights & right) != 0)
                    k ^= PolyglotBookZobrist.Castle(right);
            }

            var ep = pos.EnPassantSquare;
            if (ep.HasValue && HasEnPassantCapture(pos, ep.Value))
                k ^= PolyglotBookZobrist.EnPassant(ep.Value.File);

            if (pos.SideToMove == Color.White)
                k ^= PolyglotBookZobrist.Turn();

            return k;
        }

        private List<PolyglotEntry> LoadCandidates(IPosition pos, HashKey key)
        {
            if (!File.Exists(BookFile))
                throw new FileNotFoundException("Polyglot book file not found.", BookFile);

            using var stream = new FileStream(BookFile, FileMode.Open, FileAccess.Read, FileShare.Read);
            if (stream.Length % EntrySize != 0)
                throw new InvalidDataException("Invalid Polyglot book: file size is not a multiple of 16 bytes.");

            long entries = stream.Length / EntrySize;
            long first = FindFirstEntry(stream, entries, key.Value);
            if (first < 0) return new List<PolyglotEntry>();

            var result = new List<PolyglotEntry>(8);
            Span<byte> buf = stackalloc byte[EntrySize];

            for (long i = first; i < entries; i++)
            {
                stream.Position = i * EntrySize;
                ReadExact(stream, buf);

                ulong entryKey = BinaryPrimitives.ReadUInt64BigEndian(buf[..8]);
                if (entryKey != key.Value) break;

                ushort rawMove = BinaryPrimitives.ReadUInt16BigEndian(buf.Slice(8, 2));
                ushort weight = BinaryPrimitives.ReadUInt16BigEndian(buf.Slice(10, 2));

                var move = DecodeMove(pos, rawMove);
                if (!move.Equals(default) && pos.IsLegal(move))
                    result.Add(new PolyglotEntry(move, weight));
            }

            return result;
        }

        private static long FindFirstEntry(FileStream stream, long count, ulong targetKey)
        {
            long lo = 0;
            long hi = count - 1;
            long found = -1;
            Span<byte> keyBuf = stackalloc byte[8];

            while (lo <= hi)
            {
                long mid = lo + ((hi - lo) >> 1);
                stream.Position = mid * EntrySize;
                ReadExact(stream, keyBuf);
                ulong midKey = BinaryPrimitives.ReadUInt64BigEndian(keyBuf);

                if (midKey < targetKey)
                {
                    lo = mid + 1;
                }
                else if (midKey > targetKey)
                {
                    hi = mid - 1;
                }
                else
                {
                    found = mid;
                    hi = mid - 1;
                }
            }

            return found;
        }

        private static Move DecodeMove(IPosition pos, ushort rawMove)
        {
            int to = rawMove & 0x3F;
            int from = (rawMove >> 6) & 0x3F;
            int promotion = (rawMove >> 12) & 0x7;

            // Polyglot castling stores king move as "king captures rook".
            if (from == 4 && to == 7) to = 6;      // e1h1 -> e1g1
            else if (from == 4 && to == 0) to = 2; // e1a1 -> e1c1
            else if (from == 60 && to == 63) to = 62;   // e8h8 -> e8g8
            else if (from == 60 && to == 56) to = 58;   // e8a8 -> e8c8

            var fromSq = new Square((byte)from);
            var toSq = new Square((byte)to);
            var moving = pos.GetPiece(fromSq);
            if (moving == Piece.None) return default;

            var flags = MoveFlags.None;
            var target = pos.GetPiece(toSq);

            bool isCastle =
                moving.TypeOf() == PieceType.King &&
                ((from == 4 && (to == 6 || to == 2)) || (from == 60 && (to == 62 || to == 58)));

            if (isCastle)
            {
                flags |= MoveFlags.Castle;
            }
            else if (target != Piece.None)
            {
                flags |= MoveFlags.Capture;
            }

            if (moving.TypeOf() == PieceType.Pawn && pos.EnPassantSquare.HasValue)
            {
                Square ep = pos.EnPassantSquare.Value;
                if (toSq.Equals(ep) && target == Piece.None && fromSq.File != toSq.File)
                    flags |= MoveFlags.EnPassant | MoveFlags.Capture;
            }

            PieceType? promo = promotion switch
            {
                0 => null,
                1 => PieceType.Knight,
                2 => PieceType.Bishop,
                3 => PieceType.Rook,
                4 => PieceType.Queen,
                _ => null
            };

            if (promotion is < 0 or > 4) return default;
            return Move.Create(fromSq, toSq, flags, promo);
        }

        private static bool HasEnPassantCapture(in IPosition pos, Square ep)
        {
            if (pos.SideToMove == Color.White)
            {
                if (ep.File > 0)
                {
                    int left = ep.Index - 9;
                    if ((uint)left <= 63 && pos.GetPiece(new Square((byte)left)) == Piece.WP) return true;
                }
                if (ep.File < 7)
                {
                    int right = ep.Index - 7;
                    if ((uint)right <= 63 && pos.GetPiece(new Square((byte)right)) == Piece.WP) return true;
                }
            }
            else
            {
                if (ep.File > 0)
                {
                    int left = ep.Index + 7;
                    if ((uint)left <= 63 && pos.GetPiece(new Square((byte)left)) == Piece.BP) return true;
                }
                if (ep.File < 7)
                {
                    int right = ep.Index + 9;
                    if ((uint)right <= 63 && pos.GetPiece(new Square((byte)right)) == Piece.BP) return true;
                }
            }

            return false;
        }

        private static void ReadExact(Stream stream, Span<byte> buffer)
        {
            int read = 0;
            while (read < buffer.Length)
            {
                int n = stream.Read(buffer.Slice(read));
                if (n <= 0) throw new EndOfStreamException("Unexpected EOF in Polyglot book.");
                read += n;
            }
        }

        private static int NextRandom(int maxExclusive)
        {
#if NET6_0_OR_GREATER
            return Random.Shared.Next(maxExclusive);
#else
            if (_threadRandom == null)
            {
                int seed = System.Threading.Interlocked.Increment(ref _seed);
                _threadRandom = new Random(seed);
            }
            return _threadRandom.Next(maxExclusive);
#endif
        }

        private readonly record struct PolyglotEntry(Move Move, int Weight);
    }
}
