namespace FastChess.Library
{
    public readonly struct BoardStatePosition : IPosition
    {
        private readonly BoardState _state;

        public BoardStatePosition(in BoardState state)
        {
            _state = state;
        }

        public ulong Pieces() => _state.Occ;
        public Piece GetPiece(Square square) => _state.PieceAt(square);

        public Color SideToMove => _state.SideToMove;
        public CastlingRights CastleRights => (CastlingRights)(_state.Castling & 0x0F);
        public Square? EnPassantSquare => _state.EnPassant();

        public bool IsLegal(Move move) => _state.IsLegal(move);
    }
}
