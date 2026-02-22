using System;

namespace UnityChess.Library
{
    public sealed class GameState
    {
        public BoardState Current { get; }
        private readonly HistoryNode _head;

        private GameState(BoardState current, HistoryNode head)
        {
            Current = current;
            _head = head;
        }

        public static GameState FromFen(string fen)
        {
            var s = Fen.Parse(fen);
            return new GameState(s, new HistoryNode(s.ZobristKey, null));
        }

        public static GameState Default()
        {
            var s = Fen.Default();
            return new GameState(s, new HistoryNode(s.ZobristKey, null));
        }

        public GameState MakeMove(Move mv)
        {
            var next = Current.MakeMove(mv);

            // Optional: enforce legality here if you want a safe API
            // if (next.InCheck(Current.SideToMove)) throw new InvalidOperationException("Illegal move");

            return new GameState(next, new HistoryNode(next.ZobristKey, _head));
        }

        public bool IsDrawByFiftyMoveRule() => Current.HalfmoveClock >= 100;

        public bool IsThreefoldRepetition()
        {
            // Count current hash occurrences in the history.
            // This is O(n) but no extra allocations; good enough for UI and most engines.
            ulong target = Current.ZobristKey;

            int count = 0;
            for (HistoryNode n = _head; n != null; n = n.Prev)
            {
                if (n.Hash == target) count++;
                if (count >= 3) return true;
            }
            return false;
        }

        public bool IsDraw()
        {
            if (DrawRules.IsInsufficientMaterial(Current)) return true;

            if (IsDrawByFiftyMoveRule()) return true;
            if (IsThreefoldRepetition()) return true;

            // You can add: insufficient material; stalemate; etc.
            return false;
        }

        private sealed class HistoryNode
        {
            public readonly ulong Hash;
            public readonly HistoryNode Prev;

            public HistoryNode(ulong hash, HistoryNode prev)
            {
                Hash = hash;
                Prev = prev;
            }
        }
    }
}