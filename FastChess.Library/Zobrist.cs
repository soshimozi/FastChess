using System;

namespace FastChess.Library
{
    public static class Zobrist
    {
        // pieceIndex: 0..11 maps to Piece (WP..WK, BP..BK)
        private static readonly ulong[,] PieceSquare = new ulong[12, 64];

        private static readonly ulong SideToMoveKey;
        private static readonly ulong[] CastlingKey = new ulong[16];  // 4-bit castling rights
        private static readonly ulong[] EpFileKey = new ulong[8];     // file only, if EP exists

        static Zobrist()
        {
            // Deterministic seed; stable across runs.
            var rng = new SplitMix64(0xCAFEBABE_D15EA5E5UL);

            for (int p = 0; p < 12; p++)
                for (int sq = 0; sq < 64; sq++)
                    PieceSquare[p, sq] = rng.Next();

            SideToMoveKey = rng.Next();

            for (int i = 0; i < 16; i++)
                CastlingKey[i] = rng.Next();

            for (int f = 0; f < 8; f++)
                EpFileKey[f] = rng.Next();
        }

        public static ulong Compute(in BoardState s)
        {
            ulong z = 0;

            // Pieces
            for (int sq = 0; sq < 64; sq++)
            {
                var p = s.PieceAt(new Square((byte)sq));
                if (p == Piece.None) continue;
                z ^= PieceKey(p, sq);
            }

            // Side
            if (s.SideToMove == Color.Black) z ^= SideToMoveKey;

            // Castling
            z ^= CastlingKey[s.Castling & 0x0F];

            // EP file
            if (s.EnPassant()?.Index != 255)
            {
                int file = (s.EnPassant()?.Index ?? 0) & 7;
                z ^= EpFileKey[file];
            }

            return z;
        }

        public static ulong PieceKey(Piece p, int sq)
        {
            int idx = PieceIndex(p);
            return PieceSquare[idx, sq];
        }

        public static ulong SideKey() => SideToMoveKey;

        public static ulong CastlingRightsKey(byte rights4Bits) => CastlingKey[rights4Bits & 0x0F];

        public static ulong EpFileKeyForSquare(byte epSq)
        {
            int file = epSq & 7;
            return EpFileKey[file];
        }

        private static int PieceIndex(Piece p)
        {
            // Piece enum: None=0, WP..WK=1..6, BP..BK=7..12
            if (p == Piece.None) throw new ArgumentOutOfRangeException(nameof(p));
            return ((int)p) - 1;
        }

        private struct SplitMix64
        {
            private ulong _x;
            public SplitMix64(ulong seed) => _x = seed;

            public ulong Next()
            {
                ulong z = (_x += 0x9E3779B97F4A7C15UL);
                z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
                z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
                return z ^ (z >> 31);
            }
        }
    }
}