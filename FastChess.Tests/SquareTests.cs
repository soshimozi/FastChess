using System;
using FastChess.Library;
using Xunit;

namespace FastChess.Tests;

public class SquareTests
{
    [Fact]
    public void Constructor_WithValidIndex_SetsIndex()
    {
        var square = new Square(63);
        Assert.Equal((byte)63, square.Index);
    }

    [Fact]
    public void Constructor_WithInvalidIndex_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Square(64));
    }

    [Fact]
    public void ImplicitCastToByte_ReturnsIndex()
    {
        Square square = Square.E4;
        byte value = square;
        Assert.Equal(square.Index, value);
    }

    [Fact]
    public void FromFileRank_CreatesExpectedSquare()
    {
        var square = Square.FromFileRank(4, 3); // e4
        Assert.Equal((byte)28, square.Index);
        Assert.Equal("e4", square.ToString());
    }

    [Fact]
    public void TryParse_ValidAndInvalidInputs()
    {
        Assert.True(Square.TryParse("a1", out var a1));
        Assert.Equal(Square.A1, a1);

        Assert.False(Square.TryParse("A1", out _));
        Assert.False(Square.TryParse("i1", out _));
        Assert.False(Square.TryParse("a9", out _));
        Assert.False(Square.TryParse("e", out _));
        Assert.False(Square.TryParse(string.Empty, out _));
    }
}
