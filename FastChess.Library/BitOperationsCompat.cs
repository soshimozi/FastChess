#if NETSTANDARD2_1
namespace System.Numerics
{
    internal static class BitOperations
    {
        public static int PopCount(ulong value)
        {
            // SWAR popcount
            value -= (value >> 1) & 0x5555555555555555UL;
            value = (value & 0x3333333333333333UL) + ((value >> 2) & 0x3333333333333333UL);
            value = (value + (value >> 4)) & 0x0F0F0F0F0F0F0F0FUL;
            value *= 0x0101010101010101UL;
            return (int)(value >> 56);
        }

        public static int TrailingZeroCount(ulong value)
        {
            if (value == 0) return 64;

            int count = 0;
            while ((value & 1UL) == 0)
            {
                count++;
                value >>= 1;
            }
            return count;
        }
    }
}
#endif
