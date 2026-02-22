using UnityChess.Library;
using Xunit;

namespace UnityChess.Tests;

public class MoveTests
{
    [Fact]
    public void Create_WithoutPromotion_PacksFromToAndFlags()
    {
        var move = Move.Create(Square.E2, Square.E4, MoveFlags.None);

        Assert.Equal(Square.E2, move.From);
        Assert.Equal(Square.E4, move.To);
        Assert.Equal(MoveFlags.None, move.Flags);
        Assert.Null(move.Promotion);
        Assert.Equal("e2e4", move.ToString());
    }

    [Fact]
    public void Create_WithPromotion_SetsPromotionFlagAndPiece()
    {
        var move = Move.Create(Square.E7, Square.E8, MoveFlags.None, PieceType.Queen);

        Assert.Equal(Square.E7, move.From);
        Assert.Equal(Square.E8, move.To);
        Assert.True((move.Flags & MoveFlags.Promotion) != 0);
        Assert.Equal(PieceType.Queen, move.Promotion);
        Assert.Equal("e7e8q", move.ToString());
    }

    [Theory]
    [InlineData("e2e4", true)]
    [InlineData("a7a8q", true)]
    [InlineData("e2e9", false)]
    [InlineData("e2e4x", false)]
    [InlineData("", false)]
    public void TryParseUci_ParsesExpectedValues(string uci, bool expected)
    {
        var ok = Move.TryParseUci(uci, out var move);

        Assert.Equal(expected, ok);
        if (expected)
        {
            Assert.Equal(uci, move.ToString());
        }
    }
}
