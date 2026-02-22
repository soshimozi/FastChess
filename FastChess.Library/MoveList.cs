using System;

namespace FastChess.Library
{
    public sealed class MoveList
    {
        public const int MaxMoves = 256;

        private readonly Move[] _moves = new Move[MaxMoves];
        public int Count { get; private set; }

        public Move this[int i] => _moves[i];

        public void Clear() => Count = 0;

        public void Add(Move m)
        {
            if (Count >= MaxMoves) throw new InvalidOperationException("MoveList overflow");
            _moves[Count++] = m;
        }
    }
}