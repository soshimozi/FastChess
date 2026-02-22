using System.Numerics;

namespace FastChess.Library
{
    public static class DrawRules
    {
        // True if neither side can possibly mate with the material on board.
        // Covers: K vs K, K+N vs K, K+B vs K, K+B vs K+B (same-colored bishops)
        public static bool IsInsufficientMaterial(in BoardState s)
        {
            ulong wp = s.Pieces(Color.White, PieceType.Pawn);
            ulong bp = s.Pieces(Color.Black, PieceType.Pawn);
            if ((wp | bp) != 0) return false;

            ulong wr = s.Pieces(Color.White, PieceType.Rook);
            ulong br = s.Pieces(Color.Black, PieceType.Rook);
            if ((wr | br) != 0) return false;

            ulong wq = s.Pieces(Color.White, PieceType.Queen);
            ulong bq = s.Pieces(Color.Black, PieceType.Queen);
            if ((wq | bq) != 0) return false;

            ulong wn = s.Pieces(Color.White, PieceType.Knight);
            ulong bn = s.Pieces(Color.Black, PieceType.Knight);
            ulong wb = s.Pieces(Color.White, PieceType.Bishop);
            ulong bb = s.Pieces(Color.Black, PieceType.Bishop);

            int wN = BitOperations.PopCount(wn);
            int bN = BitOperations.PopCount(bn);
            int wB = BitOperations.PopCount(wb);
            int bB = BitOperations.PopCount(bb);

            // K vs K
            if (wN + wB + bN + bB == 0) return true;

            // K+minor vs K
            if ((wN + wB == 1) && (bN + bB == 0)) return true;
            if ((bN + bB == 1) && (wN + wB == 0)) return true;

            // K+B vs K+B only, and bishops on same color
            if (wN == 0 && bN == 0 && wB == 1 && bB == 1)
            {
                bool wColor = BishopOnLightSquare(wb);
                bool bColor = BishopOnLightSquare(bb);
                return wColor == bColor;
            }

            return false;
        }

        private static bool BishopOnLightSquare(ulong bishopBb)
        {
            // bishopBb has exactly one bit set in our use here
            int sq = BitOperations.TrailingZeroCount(bishopBb);
            int file = sq & 7;
            int rank = sq >> 3;
            // a1 is dark; light squares are (file+rank) %2 == 1
            return ((file + rank) & 1) == 1;
        }
    }
}