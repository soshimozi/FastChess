namespace UnityChess.Library
{
    public struct MoveGenIter
    {
        private readonly MoveList _list;
        private int _i;

        private MoveGenIter(MoveList list)
        {
            _list = list;
            _i = 0;
        }

        public static MoveGenIter Legal(in BoardState s, MoveList scratch)
        {
            LegalMoveGenFast.GenerateLegal(in s, scratch);
            return new MoveGenIter(scratch);
        }

        public static MoveGenIter PseudoLegal(in BoardState s, MoveList scratch)
        {
            MoveGen.GeneratePseudoLegal(in s, scratch);
            return new MoveGenIter(scratch);
        }

        public bool TryNext(out Move move)
        {
            if (_i >= _list.Count)
            {
                move = default;
                return false;
            }

            move = _list[_i++];
            return true;
        }
    }
}

/*
Usage

var scratch = new MoveList();
var gen = MoveGenIter.Legal(in state, scratch);
while (gen.TryNext(out var mv)) { ... }

*/