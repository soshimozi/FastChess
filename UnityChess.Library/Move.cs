using System;

namespace UnityChess.Library
{
    [Flags]
    public enum MoveFlags : byte
    {
        None = 0,
        Capture = 1 << 0,
        EnPassant = 1 << 1,
        Castle = 1 << 2,
        Promotion = 1 << 3
    }

    public readonly struct Move : IEquatable<Move>
    {
        // Packed layout in uint:
        // bits 0..5   from (0..63)
        // bits 6..11  to (0..63)
        // bits 12..15 promo (0 none, 1 N, 2 B, 3 R, 4 Q)
        // bits 16..23 flags (MoveFlags)
        private readonly uint _v;

        private Move(uint packed) => _v = packed;

        public Square From => new Square((byte)(_v & 63));
        public Square To => new Square((byte)((_v >> 6) & 63));
        public MoveFlags Flags => (MoveFlags)((_v >> 16) & 0xFF);

        public PieceType? Promotion
        {
            get
            {
                int p = (int)((_v >> 12) & 0xF);
                if (p == 0) return null;
                return p switch
                {
                    1 => PieceType.Knight,
                    2 => PieceType.Bishop,
                    3 => PieceType.Rook,
                    4 => PieceType.Queen,
                    _ => null
                };
            }
        }

        public static Move Create(Square from, Square to, MoveFlags flags = MoveFlags.None, PieceType? promotion = null)
        {
            uint promo = promotion switch
            {
                null => 0u,
                PieceType.Knight => 1u,
                PieceType.Bishop => 2u,
                PieceType.Rook => 3u,
                PieceType.Queen => 4u,
                _ => throw new ArgumentOutOfRangeException(nameof(promotion), "Invalid promotion piece")
            };

            if (promotion.HasValue) flags |= MoveFlags.Promotion;

            uint v = 0;
            v |= from.Index;
            v |= (uint)(to.Index << 6);
            v |= (promo << 12);
            v |= (uint)((byte)flags << 16);
            return new Move(v);
        }

        public override string ToString()
        {
            string s = From.ToString() + To.ToString();
            var promo = Promotion;
            if (promo.HasValue)
            {
                char c = promo.Value switch
                {
                    PieceType.Knight => 'n',
                    PieceType.Bishop => 'b',
                    PieceType.Rook => 'r',
                    PieceType.Queen => 'q',
                    _ => '?'
                };
                s += c;
            }
            return s;
        }

        public static bool TryParseUci(string uci, out Move move)
        {
            move = default;
            if (string.IsNullOrEmpty(uci) || (uci.Length != 4 && uci.Length != 5)) return false;

            if (!Square.TryParse(uci.Substring(0, 2), out var from)) return false;
            if (!Square.TryParse(uci.Substring(2, 2), out var to)) return false;

            PieceType? promo = null;
            if (uci.Length == 5)
            {
                promo = uci[4] switch
                {
                    'n' => PieceType.Knight,
                    'b' => PieceType.Bishop,
                    'r' => PieceType.Rook,
                    'q' => PieceType.Queen,
                    _ => (PieceType?)null
                };
                if (!promo.HasValue) return false;
            }

            move = Create(from, to, MoveFlags.None, promo);
            return true;
        }

        public bool Equals(Move other) => _v == other._v;
        public override bool Equals(object? obj) => obj is Move m && Equals(m);
        public override int GetHashCode() => (int)_v;
    }
}
