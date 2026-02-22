using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using FastChess.Library;
using Xunit;

namespace FastChess.Tests;

public class PolyglotBookTests
{
    [Fact]
    public void TryProbe_ReturnsFalse_WhenKeyNotPresent()
    {
        var state = BoardState.Default();
        IPosition pos = new BoardStatePosition(in state);
        var book = new PolyglotBook(CreateTempBookPath());

        try
        {
            var other = state.MakeMove(Move.Create(Square.E2, Square.E4));
            IPosition otherPos = new BoardStatePosition(in other);
            ulong otherKey = book.ComputePolyglotKey(in otherPos);

            WriteBook(book.BookFile, new List<BookEntry>
            {
                new(otherKey, EncodeRaw(Square.E7, Square.E5), 10, 0)
            });

            var ok = book.TryProbe(pos, out var move);
            Assert.False(ok);
            Assert.Equal(default, move);
        }
        finally
        {
            if (File.Exists(book.BookFile)) File.Delete(book.BookFile);
        }
    }

    [Fact]
    public void Probe_PickBest_ReturnsHighestWeightMove()
    {
        var state = BoardState.Default();
        IPosition pos = new BoardStatePosition(in state);
        var book = new PolyglotBook(CreateTempBookPath());

        try
        {
            ulong key = book.ComputePolyglotKey(in pos);

            WriteBook(book.BookFile, new List<BookEntry>
            {
                new(key, EncodeRaw(Square.D2, Square.D4), 10, 0),
                new(key, EncodeRaw(Square.E2, Square.E4), 30, 0)
            });

            var move = book.Probe(pos, pickBest: true);
            Assert.Equal(Move.Create(Square.E2, Square.E4), move);
        }
        finally
        {
            if (File.Exists(book.BookFile)) File.Delete(book.BookFile);
        }
    }

    [Fact]
    public void TryProbe_PickWeightedRandom_ReturnsOneOfMatchingMoves()
    {
        var state = BoardState.Default();
        IPosition pos = new BoardStatePosition(in state);
        var book = new PolyglotBook(CreateTempBookPath());

        try
        {
            ulong key = book.ComputePolyglotKey(in pos);

            var e2e4 = Move.Create(Square.E2, Square.E4);
            var d2d4 = Move.Create(Square.D2, Square.D4);

            WriteBook(book.BookFile, new List<BookEntry>
            {
                new(key, EncodeRaw(Square.D2, Square.D4), 1, 0),
                new(key, EncodeRaw(Square.E2, Square.E4), 1, 0)
            });

            var ok = book.TryProbe(pos, out var move, pickBest: false);
            Assert.True(ok);
            Assert.True(move.Equals(e2e4) || move.Equals(d2d4));
        }
        finally
        {
            if (File.Exists(book.BookFile)) File.Delete(book.BookFile);
        }
    }

    private static void WriteBook(string path, List<BookEntry> entries)
    {
        entries.Sort((a, b) => a.Key.CompareTo(b.Key));

        using var fs = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);
        Span<byte> buffer = stackalloc byte[16];

        foreach (var e in entries)
        {
            BinaryPrimitives.WriteUInt64BigEndian(buffer[..8], e.Key);
            BinaryPrimitives.WriteUInt16BigEndian(buffer.Slice(8, 2), e.Move);
            BinaryPrimitives.WriteUInt16BigEndian(buffer.Slice(10, 2), e.Weight);
            BinaryPrimitives.WriteUInt32BigEndian(buffer.Slice(12, 4), e.Learn);
            fs.Write(buffer);
        }
    }

    private static ushort EncodeRaw(Square from, Square to, int promotion = 0)
    {
        int raw = (promotion << 12) | (from.Index << 6) | to.Index;
        return (ushort)raw;
    }

    private static string CreateTempBookPath()
    {
        return Path.Combine(Path.GetTempPath(), $"fastchess-polyglot-{Guid.NewGuid():N}.bin");
    }

    private readonly record struct BookEntry(ulong Key, ushort Move, ushort Weight, uint Learn);
}
