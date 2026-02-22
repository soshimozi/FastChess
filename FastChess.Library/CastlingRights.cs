using System;

namespace FastChess.Library
{
    [Flags]
    public enum CastlingRights : byte
    {
        None = 0,
        WhiteKingSide = 1 << 0,  // K
        WhiteQueenSide = 1 << 1, // Q
        BlackKingSide = 1 << 2,  // k
        BlackQueenSide = 1 << 3  // q
    }
}