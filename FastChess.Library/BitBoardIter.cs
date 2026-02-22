using System.Numerics;

namespace FastChess.Library
{
    public struct BitboardIter
    {
        private ulong _bb;

        public BitboardIter(ulong bb) => _bb = bb;

        public bool TryPop(out Square sq)
        {
            if (_bb == 0)
            {
                sq = default;
                return false;
            }

            int i = BitOperations.TrailingZeroCount(_bb);
            _bb &= _bb - 1;
            sq = new Square((byte)i);
            return true;
        }
    }

    public static class BitboardIterExt
    {
        public static BitboardIter Iter(this ulong bb) => new BitboardIter(bb);
    }
}

/*
usage:
var it = state.Pieces(Color.White, PieceType.Bishop).Iter();
while (it.TryPop(out var sq)) { ... }
*/