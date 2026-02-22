using FastChess.Library;
using Xunit;

namespace FastChess.Tests;

public class FenTests
{
    [Fact]
    public void Parse_StartPos_HasExpectedCoreState()
    {
        var state = Fen.Parse(Fen.StartPos);

        Assert.Equal(Color.White, state.SideToMove);
        Assert.Equal((byte)0b1111, state.Castling);
        Assert.Null(state.EnPassant());
        Assert.Equal(Piece.WK, state.PieceAt(Square.E1));
        Assert.Equal(Piece.BK, state.PieceAt(Square.E8));
    }

    [Fact]
    public void ToFen_StartPos_RoundTrips()
    {
        var state = Fen.Parse(Fen.StartPos);
        var fen = Fen.ToFen(in state);
        Assert.Equal(Fen.StartPos, fen);
    }
}
