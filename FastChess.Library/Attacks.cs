using System;

namespace FastChess.Library
{
    public static class Attacks
    {
        public static readonly ulong[] Knight = new ulong[64];
        public static readonly ulong[] King = new ulong[64];
        public static readonly ulong[,] Pawn = new ulong[2, 64]; // [color, sq]

        static Attacks()
        {
            InitLeapers();
        }

        private static void InitLeapers()
        {
            for (int sq = 0; sq < 64; sq++)
            {
                int f = sq & 7;
                int r = sq >> 3;

                Knight[sq] = BuildKnight(f, r);
                King[sq] = BuildKing(f, r);

                Pawn[(int)Color.White, sq] = BuildPawnAttacks(Color.White, f, r);
                Pawn[(int)Color.Black, sq] = BuildPawnAttacks(Color.Black, f, r);
            }
        }

        private static ulong BuildKnight(int f, int r)
        {
            ulong bb = 0;
            Add(ref bb, f + 1, r + 2);
            Add(ref bb, f + 2, r + 1);
            Add(ref bb, f + 2, r - 1);
            Add(ref bb, f + 1, r - 2);
            Add(ref bb, f - 1, r - 2);
            Add(ref bb, f - 2, r - 1);
            Add(ref bb, f - 2, r + 1);
            Add(ref bb, f - 1, r + 2);
            return bb;
        }

        private static ulong BuildKing(int f, int r)
        {
            ulong bb = 0;
            for (int df = -1; df <= 1; df++)
            for (int dr = -1; dr <= 1; dr++)
            {
                if (df == 0 && dr == 0) continue;
                Add(ref bb, f + df, r + dr);
            }
            return bb;
        }

        private static ulong BuildPawnAttacks(Color c, int f, int r)
        {
            ulong bb = 0;
            int dr = c == Color.White ? 1 : -1;
            Add(ref bb, f - 1, r + dr);
            Add(ref bb, f + 1, r + dr);
            return bb;
        }

        private static void Add(ref ulong bb, int f, int r)
        {
            if ((uint)f > 7 || (uint)r > 7) return;
            int sq = r * 8 + f;
            bb |= 1UL << sq;
        }

        // Sliding attacks via Magic tables (generated; embedded)
        public static ulong Rook(int sq, ulong occ) => Magic.RookAttacks(sq, occ);
        public static ulong Bishop(int sq, ulong occ) => Magic.BishopAttacks(sq, occ);
        public static ulong Queen(int sq, ulong occ) => Rook(sq, occ) | Bishop(sq, occ);
    }
}