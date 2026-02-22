using System;
using FastChess.Core;

namespace FastChess.Library
{
    public static class Magic
    {
        public static ulong[] RookMask { get; private set; } = Array.Empty<ulong>();
        public static ulong[] BishopMask { get; private set; } = Array.Empty<ulong>();

        public static ulong[] RookMagic { get; private set; } = Array.Empty<ulong>();
        public static ulong[] BishopMagic { get; private set; } = Array.Empty<ulong>();

        public static byte[] RookShift { get; private set; } = Array.Empty<byte>();
        public static byte[] BishopShift { get; private set; } = Array.Empty<byte>();

        public static int[] RookOffset { get; private set; } = Array.Empty<int>();
        public static int[] BishopOffset { get; private set; } = Array.Empty<int>();

        public static ulong[] RookTable { get; private set; } = Array.Empty<ulong>();
        public static ulong[] BishopTable { get; private set; } = Array.Empty<ulong>();

        static Magic()
        {
            // After you generate + paste MagicData arrays, wire them here:
            if (MagicData.RookTable != null && MagicData.RookTable.Length != 0)
            {
                RookMask = MagicData.RookMask;
                BishopMask = MagicData.BishopMask;

                RookMagic = MagicData.RookMagic;
                BishopMagic = MagicData.BishopMagic;

                RookShift = MagicData.RookShift;
                BishopShift = MagicData.BishopShift;

                RookOffset = MagicData.RookOffset;
                BishopOffset = MagicData.BishopOffset;

                RookTable = MagicData.RookTable;
                BishopTable = MagicData.BishopTable;
            }
        }

        public static void ValidateReady()
        {
            if (RookTable.Length == 0 || BishopTable.Length == 0)
                throw new InvalidOperationException("Magic tables not initialized. Generate and embed MagicData.");
        }

        public static ulong RookAttacks(int sq, ulong occ)
        {
            occ &= RookMask[sq];
            ulong idx = (occ * RookMagic[sq]) >> RookShift[sq];
            return RookTable[RookOffset[sq] + (int)idx];
        }

        public static ulong BishopAttacks(int sq, ulong occ)
        {
            occ &= BishopMask[sq];
            ulong idx = (occ * BishopMagic[sq]) >> BishopShift[sq];
            return BishopTable[BishopOffset[sq] + (int)idx];
        }
    }
}