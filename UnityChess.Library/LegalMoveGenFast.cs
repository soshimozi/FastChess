using System;
using System.Numerics;

namespace UnityChess.Library
{
    public static class LegalMoveGenFast
    {
        // Public entry point: fills list with LEGAL moves (fast pin/check masking).
        // En passant moves are validated via immutable MakeMove because of the rare discovered-check EP edge case.
        public static void GenerateLegal(in BoardState s, MoveList list)
        {
            list.Clear();

            Color us = s.SideToMove;
            Color them = us.Opp();

            ulong usOcc = us == Color.White ? s.WhiteOcc : s.BlackOcc;
            ulong themOcc = us == Color.White ? s.BlackOcc : s.WhiteOcc;
            ulong occ = usOcc | themOcc;

            int ksq = s.KingSquare(us).Index;

            // Compute checkers and pin info.
            ComputePinsAndChecks(in s, us, them, ksq, usOcc, themOcc, occ,
                out ulong checkers, out int checkCount,
                out ulong checkMask, // squares that can resolve check (capture checker or block); all ones if not in check
                out ulong pinned,    // bitboard of our pinned pieces
                out ulong[] pinLine  // per-square allowed squares mask for pinned pieces
            );

            // If double-check: only king moves
            if (checkCount >= 2)
            {
                GenKingLegal(in s, list, us, them, ksq, usOcc, themOcc, occ);
                return;
            }

            // King legal moves always generated; additionally, if in check, king moves are one resolution.
            GenKingLegal(in s, list, us, them, ksq, usOcc, themOcc, occ);

            // Generate non-king moves with masks:
            // - if in check: destinations must be within checkMask
            // - if pinned: destinations must also be within pinLine[fromSq]
            // - captures and quiet moves already handled by pseudo generation style
            ulong destMask = checkMask; // if not in check => all ones

            // Knights
            GenKnights(in s, list, usOcc, themOcc, pinned, pinLine, destMask);

            // Bishops, rooks, queens (magic attacks)
            GenSliders(in s, list, usOcc, themOcc, occ, pinned, pinLine, destMask);

            // Pawns (includes promotions; EP added separately + validated)
            GenPawns(in s, list, us, usOcc, themOcc, occ, pinned, pinLine, destMask);

            // Castling (only if not in check; your existing generator already checks attacked squares)
            if (checkCount == 0)
            {
                GenCastling(in s, list);
            }

            // En passant: generate pseudo EP moves that satisfy pin/check masks, then validate with MakeMove
            GenEnPassantValidated(in s, list, us, themOcc, pinned, pinLine, destMask);
        }

        private static void GenKingLegal(in BoardState s, MoveList list, Color us, Color them, int ksq, ulong usOcc, ulong themOcc, ulong occ)
        {
            ulong attacks = Attacks.King[ksq] & ~usOcc;
            while (attacks != 0)
            {
                int to = BitOperations.TrailingZeroCount(attacks);
                attacks &= attacks - 1;

                // King can't move into check
                if (s.IsSquareAttacked(new Square((byte)to), them)) continue;

                var flags = ((themOcc >> to) & 1) != 0 ? MoveFlags.Capture : MoveFlags.None;
                list.Add(Move.Create(new Square((byte)ksq), new Square((byte)to), flags));
            }
        }

        private static void GenKnights(in BoardState s, MoveList list, ulong usOcc, ulong themOcc,
            ulong pinned, ulong[] pinLine, ulong destMask)
        {
            ulong knights = s.Pieces(s.SideToMove, PieceType.Knight);

            // pinned knights cannot move (they would break line)
            knights &= ~pinned;

            while (knights != 0)
            {
                int from = BitOperations.TrailingZeroCount(knights);
                knights &= knights - 1;

                ulong moves = Attacks.Knight[from] & ~usOcc & destMask;
                while (moves != 0)
                {
                    int to = BitOperations.TrailingZeroCount(moves);
                    moves &= moves - 1;

                    var flags = ((themOcc >> to) & 1) != 0 ? MoveFlags.Capture : MoveFlags.None;
                    list.Add(Move.Create(new Square((byte)from), new Square((byte)to), flags));
                }
            }
        }

        private static void GenSliders(in BoardState s, MoveList list, ulong usOcc, ulong themOcc, ulong occ,
            ulong pinned, ulong[] pinLine, ulong destMask)
        {
            GenSliderType(in s, list, PieceType.Bishop, usOcc, themOcc, occ, pinned, pinLine, destMask);
            GenSliderType(in s, list, PieceType.Rook, usOcc, themOcc, occ, pinned, pinLine, destMask);
            GenSliderType(in s, list, PieceType.Queen, usOcc, themOcc, occ, pinned, pinLine, destMask);
        }

        private static void GenSliderType(in BoardState s, MoveList list, PieceType pt, ulong usOcc, ulong themOcc, ulong occ,
            ulong pinned, ulong[] pinLine, ulong destMask)
        {
            ulong bb = s.Pieces(s.SideToMove, pt);

            while (bb != 0)
            {
                int from = BitOperations.TrailingZeroCount(bb);
                bb &= bb - 1;

                ulong allowed = destMask;
                if (((pinned >> from) & 1) != 0)
                    allowed &= pinLine[from];

                ulong attacks = pt switch
                {
                    PieceType.Bishop => Attacks.Bishop(from, occ),
                    PieceType.Rook => Attacks.Rook(from, occ),
                    PieceType.Queen => Attacks.Queen(from, occ),
                    _ => 0
                };

                attacks &= ~usOcc;
                attacks &= allowed;

                while (attacks != 0)
                {
                    int to = BitOperations.TrailingZeroCount(attacks);
                    attacks &= attacks - 1;

                    var flags = ((themOcc >> to) & 1) != 0 ? MoveFlags.Capture : MoveFlags.None;
                    list.Add(Move.Create(new Square((byte)from), new Square((byte)to), flags));
                }
            }
        }

        private static void GenPawns(in BoardState s, MoveList list, Color us, ulong usOcc, ulong themOcc, ulong occ,
            ulong pinned, ulong[] pinLine, ulong destMask)
        {
            bool white = us == Color.White;
            ulong pawns = s.Pieces(us, PieceType.Pawn);
            ulong empty = ~occ;

            // For pinned pawns, we apply per-from filtering by expanding moves from each pawn.
            // It’s still fast because pawn count is small; keeps logic simple and correct.
            var it = pawns.Iter();
            while (it.TryPop(out var fromSq))
            {
                int from = fromSq.Index;

                ulong allowed = destMask;
                if (((pinned >> from) & 1) != 0)
                    allowed &= pinLine[from];

                int fromRank = from >> 3;
                int fromFile = from & 7;

                if (white)
                {
                    // Push 1
                    int to1 = from + 8;
                    if (to1 <= 63 && ((occ >> to1) & 1) == 0 && (((allowed >> to1) & 1) != 0))
                    {
                        AddPawnMove(list, from, to1, isCapture: false, promoRank: 7);
                        // Push 2 from rank 2 (rank==1)
                        if (fromRank == 1)
                        {
                            int to2 = from + 16;
                            if (((occ >> to2) & 1) == 0 && ((occ >> (from + 8)) & 1) == 0 && (((allowed >> to2) & 1) != 0))
                                list.Add(Move.Create(new Square((byte)from), new Square((byte)to2)));
                        }
                    }

                    // Captures
                    if (fromFile != 0)
                    {
                        int cap = from + 7;
                        if (cap <= 63 && ((themOcc >> cap) & 1) != 0 && (((allowed >> cap) & 1) != 0))
                            AddPawnMove(list, from, cap, isCapture: true, promoRank: 7);
                    }
                    if (fromFile != 7)
                    {
                        int cap = from + 9;
                        if (cap <= 63 && ((themOcc >> cap) & 1) != 0 && (((allowed >> cap) & 1) != 0))
                            AddPawnMove(list, from, cap, isCapture: true, promoRank: 7);
                    }
                }
                else
                {
                    // Push 1
                    int to1 = from - 8;
                    if (to1 >= 0 && ((occ >> to1) & 1) == 0 && (((allowed >> to1) & 1) != 0))
                    {
                        AddPawnMove(list, from, to1, isCapture: false, promoRank: 0);
                        // Push 2 from rank 7 (rank==6)
                        if (fromRank == 6)
                        {
                            int to2 = from - 16;
                            if (((occ >> to2) & 1) == 0 && ((occ >> (from - 8)) & 1) == 0 && (((allowed >> to2) & 1) != 0))
                                list.Add(Move.Create(new Square((byte)from), new Square((byte)to2)));
                        }
                    }

                    // Captures
                    if (fromFile != 0)
                    {
                        int cap = from - 9;
                        if (cap >= 0 && ((themOcc >> cap) & 1) != 0 && (((allowed >> cap) & 1) != 0))
                            AddPawnMove(list, from, cap, isCapture: true, promoRank: 0);
                    }
                    if (fromFile != 7)
                    {
                        int cap = from - 7;
                        if (cap >= 0 && ((themOcc >> cap) & 1) != 0 && (((allowed >> cap) & 1) != 0))
                            AddPawnMove(list, from, cap, isCapture: true, promoRank: 0);
                    }
                }
            }
        }

        private static void AddPawnMove(MoveList list, int from, int to, bool isCapture, int promoRank)
        {
            int toRank = to >> 3;
            var flags = isCapture ? MoveFlags.Capture : MoveFlags.None;

            if (toRank == promoRank)
            {
                list.Add(Move.Create(new Square((byte)from), new Square((byte)to), flags | MoveFlags.Promotion, PieceType.Queen));
                list.Add(Move.Create(new Square((byte)from), new Square((byte)to), flags | MoveFlags.Promotion, PieceType.Rook));
                list.Add(Move.Create(new Square((byte)from), new Square((byte)to), flags | MoveFlags.Promotion, PieceType.Bishop));
                list.Add(Move.Create(new Square((byte)from), new Square((byte)to), flags | MoveFlags.Promotion, PieceType.Knight));
            }
            else
            {
                list.Add(Move.Create(new Square((byte)from), new Square((byte)to), flags));
            }
        }

        private static void GenCastling(in BoardState s, MoveList list)
        {
            // Reuse the exact logic you already added inside MoveGen.GenKing for castling generation.
            // To avoid duplication, simplest is: call your existing helper by temporarily generating only castling.
            // Here we inline the same rules (safe and fast).

            Color us = s.SideToMove;
            Color them = us.Opp();

            if (us == Color.White)
            {
                int e1 = 4, f1 = 5, g1 = 6, d1 = 3, c1 = 2, b1 = 1;
                if ((s.Castling & 0b0001) != 0)
                {
                    ulong occ = s.Occ;
                    if (((occ >> f1) & 1) == 0 && ((occ >> g1) & 1) == 0)
                    {
                        if (!s.IsSquareAttacked(new Square((byte)e1), them) &&
                            !s.IsSquareAttacked(new Square((byte)f1), them) &&
                            !s.IsSquareAttacked(new Square((byte)g1), them))
                        {
                            list.Add(Move.Create(new Square((byte)e1), new Square((byte)g1), MoveFlags.Castle));
                        }
                    }
                }
                if ((s.Castling & 0b0010) != 0)
                {
                    ulong occ = s.Occ;
                    if (((occ >> d1) & 1) == 0 && ((occ >> c1) & 1) == 0 && ((occ >> b1) & 1) == 0)
                    {
                        if (!s.IsSquareAttacked(new Square((byte)e1), them) &&
                            !s.IsSquareAttacked(new Square((byte)d1), them) &&
                            !s.IsSquareAttacked(new Square((byte)c1), them))
                        {
                            list.Add(Move.Create(new Square((byte)e1), new Square((byte)c1), MoveFlags.Castle));
                        }
                    }
                }
            }
            else
            {
                int e8 = 60, f8 = 61, g8 = 62, d8 = 59, c8 = 58, b8 = 57;
                if ((s.Castling & 0b0100) != 0)
                {
                    ulong occ = s.Occ;
                    if (((occ >> f8) & 1) == 0 && ((occ >> g8) & 1) == 0)
                    {
                        if (!s.IsSquareAttacked(new Square((byte)e8), them) &&
                            !s.IsSquareAttacked(new Square((byte)f8), them) &&
                            !s.IsSquareAttacked(new Square((byte)g8), them))
                        {
                            list.Add(Move.Create(new Square((byte)e8), new Square((byte)g8), MoveFlags.Castle));
                        }
                    }
                }
                if ((s.Castling & 0b1000) != 0)
                {
                    ulong occ = s.Occ;
                    if (((occ >> d8) & 1) == 0 && ((occ >> c8) & 1) == 0 && ((occ >> b8) & 1) == 0)
                    {
                        if (!s.IsSquareAttacked(new Square((byte)e8), them) &&
                            !s.IsSquareAttacked(new Square((byte)d8), them) &&
                            !s.IsSquareAttacked(new Square((byte)c8), them))
                        {
                            list.Add(Move.Create(new Square((byte)e8), new Square((byte)c8), MoveFlags.Castle));
                        }
                    }
                }
            }
        }

        private static void GenEnPassantValidated(in BoardState s, MoveList list, Color us,
            ulong themOcc, ulong pinned, ulong[] pinLine, ulong destMask)
        {
            if (s.EnPassant()?.Index == 255) return;

            int epSq = s.EnPassant()?.Index ?? 0;
            ulong epBit = 1UL << epSq;

            // In-check constraint applies to EP destination too
            if (((destMask >> epSq) & 1) == 0) return;

            ulong pawns = s.Pieces(us, PieceType.Pawn);

            // Candidates by geometry; then apply pinLine (if pinned) and validate via MakeMove+InCheck
            Span<int> froms = stackalloc int[2];
            int count = 0;

            if (us == Color.White)
            {
                if ((epSq & 7) != 0) froms[count++] = epSq - 9;
                if ((epSq & 7) != 7) froms[count++] = epSq - 7;
            }
            else
            {
                if ((epSq & 7) != 0) froms[count++] = epSq + 7;
                if ((epSq & 7) != 7) froms[count++] = epSq + 9;
            }

            for (int i = 0; i < count; i++)
            {
                int from = froms[i];
                if ((uint)from > 63) continue;
                if (((pawns >> from) & 1) == 0) continue;

                ulong allowed = destMask;
                if (((pinned >> from) & 1) != 0)
                    allowed &= pinLine[from];

                if (((allowed >> epSq) & 1) == 0) continue;

                var mv = Move.Create(new Square((byte)from), new Square((byte)epSq), MoveFlags.EnPassant | MoveFlags.Capture);

                // Validate rare discovered-check EP case:
                var ns = s.MakeMove(mv);
                if (!ns.InCheck(us))
                    list.Add(mv);
            }
        }

        private static void ComputePinsAndChecks(
            in BoardState s,
            Color us,
            Color them,
            int ksq,
            ulong usOcc,
            ulong themOcc,
            ulong occ,
            out ulong checkers,
            out int checkCount,
            out ulong checkMask,
            out ulong pinned,
            out ulong[] pinLine)
        {
            pinLine = PinLinePool.Rent(); // 64-length array
            Array.Fill(pinLine, ~0UL);     // default: no restriction

            checkers = 0;
            checkCount = 0;
            pinned = 0;

            // Start with all squares allowed if not in check
            checkMask = ~0UL;

            // Knight checkers
            ulong n = s.Pieces(them, PieceType.Knight);
            ulong nChecks = Attacks.Knight[ksq] & n;
            if (nChecks != 0)
            {
                checkers |= nChecks;
                checkCount += BitOperations.PopCount(nChecks);
            }

            // Pawn checkers: squares from which opponent pawns attack king
            ulong p = s.Pieces(them, PieceType.Pawn);
            ulong pChecks = Attacks.Pawn[(int)us, ksq] & p;
            if (pChecks != 0)
            {
                checkers |= pChecks;
                checkCount += BitOperations.PopCount(pChecks);
            }

            // Sliding checkers + pins (by ray-walking from king)
            // Directions: N,S,E,W, NE,NW,SE,SW
            RayWalk(in s, us, them, ksq, usOcc, themOcc, occ, isDiag: false, ref checkers, ref checkCount, ref pinned, pinLine);
            RayWalk(in s, us, them, ksq, usOcc, themOcc, occ, isDiag: true, ref checkers, ref checkCount, ref pinned, pinLine);

            // If in check: build checkMask = capture checker OR block slider checks (or king moves already handled)
            if (checkCount == 0)
            {
                checkMask = ~0UL;
                return;
            }

            // If multiple checkers, checkMask is irrelevant (only king moves handled earlier)
            if (checkCount >= 2)
            {
                checkMask = 0; // unused
                return;
            }

            // Exactly one checker
            int checkerSq = BitOperations.TrailingZeroCount(checkers);
            ulong mask = 1UL << checkerSq;

            // If checker is slider, allow blocking squares between king and checker (inclusive checker square)
            var checkerPiece = s.PieceAt(new Square((byte)checkerSq));
            bool slider = checkerPiece.TypeOf() is PieceType.Bishop or PieceType.Rook or PieceType.Queen;

            if (slider)
            {
                ulong between = SquaresBetweenInclusive(ksq, checkerSq);
                checkMask = between;
            }
            else
            {
                // capture only
                checkMask = mask;
            }
        }

        private static void RayWalk(
            in BoardState s, Color us, Color them, int ksq,
            ulong usOcc, ulong themOcc, ulong occ,
            bool isDiag,
            ref ulong checkers, ref int checkCount,
            ref ulong pinned,
            ulong[] pinLine)
        {
            // Rook-like directions
            if (!isDiag)
            {
                WalkDir(in s, us, them, ksq, +8, usOcc, themOcc, occ, sliderMask: s.Pieces(them, PieceType.Rook) | s.Pieces(them, PieceType.Queen),
                    ref checkers, ref checkCount, ref pinned, pinLine);
                WalkDir(in s, us, them, ksq, -8, usOcc, themOcc, occ, sliderMask: s.Pieces(them, PieceType.Rook) | s.Pieces(them, PieceType.Queen),
                    ref checkers, ref checkCount, ref pinned, pinLine);
                WalkDir(in s, us, them, ksq, +1, usOcc, themOcc, occ, sliderMask: s.Pieces(them, PieceType.Rook) | s.Pieces(them, PieceType.Queen),
                    ref checkers, ref checkCount, ref pinned, pinLine);
                WalkDir(in s, us, them, ksq, -1, usOcc, themOcc, occ, sliderMask: s.Pieces(them, PieceType.Rook) | s.Pieces(them, PieceType.Queen),
                    ref checkers, ref checkCount, ref pinned, pinLine);
            }
            else
            {
                WalkDir(in s, us, them, ksq, +9, usOcc, themOcc, occ, sliderMask: s.Pieces(them, PieceType.Bishop) | s.Pieces(them, PieceType.Queen),
                    ref checkers, ref checkCount, ref pinned, pinLine);
                WalkDir(in s, us, them, ksq, +7, usOcc, themOcc, occ, sliderMask: s.Pieces(them, PieceType.Bishop) | s.Pieces(them, PieceType.Queen),
                    ref checkers, ref checkCount, ref pinned, pinLine);
                WalkDir(in s, us, them, ksq, -7, usOcc, themOcc, occ, sliderMask: s.Pieces(them, PieceType.Bishop) | s.Pieces(them, PieceType.Queen),
                    ref checkers, ref checkCount, ref pinned, pinLine);
                WalkDir(in s, us, them, ksq, -9, usOcc, themOcc, occ, sliderMask: s.Pieces(them, PieceType.Bishop) | s.Pieces(them, PieceType.Queen),
                    ref checkers, ref checkCount, ref pinned, pinLine);
            }
        }

        private static void WalkDir(
            in BoardState s, Color us, Color them, int ksq, int delta,
            ulong usOcc, ulong themOcc, ulong occ,
            ulong sliderMask,
            ref ulong checkers, ref int checkCount,
            ref ulong pinned, ulong[] pinLine)
        {
            int from = ksq;
            int prevFile = from & 7;

            int firstOwnSq = -1;
            ulong ray = 0;

            while (true)
            {
                int next = from + delta;
                if ((uint)next > 63) break;

                // prevent wrap across files for +/-1 and diagonals
                int nextFile = next & 7;
                int df = nextFile - prevFile;
                if (delta == +1 || delta == -1)
                {
                    if (Math.Abs(df) != 1) break;
                }
                else if (delta == +9 || delta == -9 || delta == +7 || delta == -7)
                {
                    if (Math.Abs(df) != 1) break;
                }

                ray |= 1UL << next;

                ulong bit = 1UL << next;
                bool isOcc = (occ & bit) != 0;
                if (!isOcc)
                {
                    from = next;
                    prevFile = nextFile;
                    continue;
                }

                bool isOwn = (usOcc & bit) != 0;
                if (isOwn)
                {
                    if (firstOwnSq == -1)
                    {
                        firstOwnSq = next;
                        from = next;
                        prevFile = nextFile;
                        continue;
                    }
                    // second own piece blocks; no pin/check in this direction
                    break;
                }
                else
                {
                    // enemy piece encountered
                    if ((sliderMask & bit) != 0)
                    {
                        if (firstOwnSq == -1)
                        {
                            // direct slider check on king
                            checkers |= bit;
                            checkCount++;
                        }
                        else
                        {
                            // pin: firstOwnSq is pinned along this ray; allowed moves are along king<->attacker line
                            pinned |= 1UL << firstOwnSq;
                            pinLine[firstOwnSq] = ray; // includes squares up to attacker (inclusive) but not king square; that’s fine
                        }
                    }
                    break;
                }
            }
        }

        private static ulong SquaresBetweenInclusive(int a, int b)
        {
            // Returns bitboard containing squares between a and b inclusive (including b; excluding a is fine for our usage).
            // Assumes a and b are aligned (same rank/file/diag).
            int af = a & 7, ar = a >> 3;
            int bf = b & 7, br = b >> 3;

            int df = Math.Sign(bf - af);
            int dr = Math.Sign(br - ar);

            // Validate alignment
            if (!(df == 0 || dr == 0 || Math.Abs(bf - af) == Math.Abs(br - ar)))
                return 0;

            ulong bb = 0;
            int f = af, r = ar;
            while (true)
            {
                f += df; r += dr;
                if ((uint)f > 7 || (uint)r > 7) break;
                int sq = r * 8 + f;
                bb |= 1UL << sq;
                if (sq == b) break;
            }
            return bb;
        }

        private static class PinLinePool
        {
            [ThreadStatic] private static ulong[]? _arr;
            public static ulong[] Rent()
            {
                _arr ??= new ulong[64];
                return _arr;
            }
        }
    }
}
