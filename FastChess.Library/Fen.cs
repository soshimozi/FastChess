using System;
using System.Text;

namespace FastChess.Library
{
    public static class Fen
    {
        public const string StartPos =
            "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1";

        public static BoardState Default() => Parse(StartPos);

        public static BoardState Parse(string fen)
        {
            if (fen == null) throw new ArgumentNullException(nameof(fen));
            var parts = fen.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 4) throw new FormatException("FEN must have at least 4 fields.");

            string placement = parts[0];
            string stm = parts[1];
            string castlingStr = parts[2];
            string epStr = parts[3];
            ushort halfmove = parts.Length >= 5 ? ParseUShort(parts[4]) : (ushort)0;
            ushort fullmove = parts.Length >= 6 ? ParseUShort(parts[5]) : (ushort)1;

            ulong wp = 0, wn = 0, wb = 0, wr = 0, wq = 0, wk = 0;
            ulong bp = 0, bn = 0, bb = 0, br = 0, bq = 0, bk = 0;

            int rank = 7;
            int file = 0;

            foreach (char c in placement)
            {
                if (c == '/')
                {
                    if (file != 8) throw new FormatException("Bad FEN placement row width.");
                    rank--;
                    file = 0;
                    continue;
                }

                if (c >= '1' && c <= '8')
                {
                    file += c - '0';
                    if (file > 8) throw new FormatException("Bad FEN digit overflow.");
                    continue;
                }

                if (rank < 0 || file < 0 || file > 7) throw new FormatException("Bad FEN placement.");
                int sq = rank * 8 + file;
                ulong mask = 1UL << sq;

                switch (c)
                {
                    case 'P': wp |= mask; break;
                    case 'N': wn |= mask; break;
                    case 'B': wb |= mask; break;
                    case 'R': wr |= mask; break;
                    case 'Q': wq |= mask; break;
                    case 'K': wk |= mask; break;

                    case 'p': bp |= mask; break;
                    case 'n': bn |= mask; break;
                    case 'b': bb |= mask; break;
                    case 'r': br |= mask; break;
                    case 'q': bq |= mask; break;
                    case 'k': bk |= mask; break;

                    default:
                        throw new FormatException($"Invalid piece char '{c}' in FEN.");
                }

                file++;
            }

            if (rank != 0 || file != 8)
                throw new FormatException("Bad FEN placement termination.");

            Color sideToMove = stm switch
            {
                "w" => Color.White,
                "b" => Color.Black,
                _ => throw new FormatException("Bad FEN side-to-move.")
            };

            byte castling = 0;
            if (castlingStr != "-")
            {
                foreach (char c in castlingStr)
                {
                    castling |= c switch
                    {
                        'K' => (byte)0b0001,
                        'Q' => (byte)0b0010,
                        'k' => (byte)0b0100,
                        'q' => (byte)0b1000,
                        _ => throw new FormatException("Bad FEN castling rights.")
                    };
                }
            }

            byte ep = 255;
            if (epStr != "-")
            {
                if (!Square.TryParse(epStr, out var epSq))
                    throw new FormatException("Bad FEN en-passant square.");
                ep = epSq.Index;
            }

            var s0 = new BoardState(wp, wn, wb, wr, wq, wk, bp, bn, bb, br, bq, bk,
                sideToMove, castling, ep, halfmove, fullmove, 0);

            ulong zob = Zobrist.Compute(in s0);
            return s0.WithZobrist(zob);
        }

        public static string ToFen(in BoardState s)
        {
            var sb = new StringBuilder(128);

            for (int rank = 7; rank >= 0; rank--)
            {
                int empty = 0;
                for (int file = 0; file < 8; file++)
                {
                    int sq = rank * 8 + file;
                    var p = s.PieceAt(new Square((byte)sq));
                    if (p == Piece.None)
                    {
                        empty++;
                        continue;
                    }

                    if (empty != 0)
                    {
                        sb.Append(empty);
                        empty = 0;
                    }

                    sb.Append(PieceToChar(p));
                }

                if (empty != 0) sb.Append(empty);
                if (rank != 0) sb.Append('/');
            }

            sb.Append(' ');
            sb.Append(s.SideToMove == Color.White ? "w" : "b");
            sb.Append(' ');

            if (s.Castling == 0) sb.Append('-');
            else
            {
                if ((s.Castling & 0b0001) != 0) sb.Append('K');
                if ((s.Castling & 0b0010) != 0) sb.Append('Q');
                if ((s.Castling & 0b0100) != 0) sb.Append('k');
                if ((s.Castling & 0b1000) != 0) sb.Append('q');
            }

            sb.Append(' ');
            var enPassant = s.EnPassant();
            sb.Append(enPassant.HasValue ? enPassant.Value.ToString() : "-");

            sb.Append(' ');
            sb.Append(s.HalfmoveClock);
            sb.Append(' ');
            sb.Append(s.FullmoveNumber);

            return sb.ToString();
        }

        private static char PieceToChar(Piece p) => p switch
        {
            Piece.WP => 'P',
            Piece.WN => 'N',
            Piece.WB => 'B',
            Piece.WR => 'R',
            Piece.WQ => 'Q',
            Piece.WK => 'K',

            Piece.BP => 'p',
            Piece.BN => 'n',
            Piece.BB => 'b',
            Piece.BR => 'r',
            Piece.BQ => 'q',
            Piece.BK => 'k',

            _ => '?'
        };

        private static ushort ParseUShort(string s)
        {
            if (!ushort.TryParse(s, out var v)) throw new FormatException("Bad numeric field in FEN.");
            return v;
        }
    }
}
