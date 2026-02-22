namespace FastChess.Library
{
    public interface IPosition
    {
        ulong Pieces();
        Piece GetPiece(Square square);

        Color SideToMove { get; }
        CastlingRights CastleRights { get; }
        Square? EnPassantSquare { get; }

        bool IsLegal(Move move);
    }
}
