using UnityChess.Library;

Console.WriteLine("Hello, World!");

var state = GameState.Default();
var moves = new MoveList();

var gen = MoveGenIter.Legal(state.Current, moves);
while (gen.TryNext(out var mv))
{
    Console.WriteLine(mv);
}

var move1 = Move.Create(Square.D2, Square.D4, MoveFlags.None, null);
var move2 = Move.Create(Square.H7, Square.H5, MoveFlags.None, null);
var move3 = Move.Create(Square.D4, Square.D5, MoveFlags.None, null);
var move4 = Move.Create(Square.E7, Square.E5, MoveFlags.None, null);

var board = BoardState.Default().MakeMove(move1).MakeMove(move2).MakeMove(move3).MakeMove(move4);


var fen = Fen.ToFen(board);
Console.WriteLine(fen);

var enspassant = board.EnPassant();
if (enspassant.HasValue)
{
    Console.WriteLine(enspassant.Value);
}




