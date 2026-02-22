using System;

namespace FastChess.Library
{
    public static class Perft
    {
        // Counts leaf nodes at depth. Depth 0 = 1.
        public static long Run(in BoardState s, int depth)
        {
            if (depth < 0) throw new ArgumentOutOfRangeException(nameof(depth));
            if (depth == 0) return 1;

            var moves = new MoveList();
            MoveGen.GenerateLegal(in s, moves);

            long nodes = 0;
            for (int i = 0; i < moves.Count; i++)
            {
                var ns = s.MakeMove(moves[i]);
                nodes += Run(in ns, depth - 1);
            }
            return nodes;
        }

        public static void SelfTest()
        {
            // Start position known perft:
            // d1 20, d2 400, d3 8902, d4 197281, d5 4865609, d6 119060324
            var start = Fen.Default();
            Assert(start, 1, 20);
            Assert(start, 2, 400);
            Assert(start, 3, 8902);
            Assert(start, 4, 197281);

            // Kiwipete (classic castling/ep/pins coverage)
            var kiwipete = Fen.Parse("r3k2r/p1ppqpb1/bn2pnp1/3PN3/1p2P3/2N2Q1p/PPPBBPPP/R3K2R w KQkq - 0 1");
            Assert(kiwipete, 1, 48);
            Assert(kiwipete, 2, 2039);
            Assert(kiwipete, 3, 97862);
        }

        private static void Assert(in BoardState s, int depth, long expected)
        {
            long got = Run(in s, depth);
            if (got != expected)
                throw new Exception($"Perft failed depth {depth}: expected {expected}, got {got}");
        }
    }
}