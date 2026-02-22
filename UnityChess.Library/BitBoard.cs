using System;
using System.Numerics;

namespace UnityChess.Library
{
    public readonly struct Bitboard : IEquatable<Bitboard>
    {
        public readonly ulong Value;
        public Bitboard(ulong value) => Value = value;

        public bool Any => Value != 0;

        public static Bitboard operator |(Bitboard a, Bitboard b) => new Bitboard(a.Value | b.Value);
        public static Bitboard operator &(Bitboard a, Bitboard b) => new Bitboard(a.Value & b.Value);
        public static Bitboard operator ^(Bitboard a, Bitboard b) => new Bitboard(a.Value ^ b.Value);
        public static Bitboard operator ~(Bitboard a) => new Bitboard(~a.Value);

        public static implicit operator Bitboard(ulong v) => new Bitboard(v);
        public static implicit operator ulong(Bitboard b) => b.Value;

        public int PopCount() => BitOperations.PopCount(Value);

        public Square PopLsb()
        {
            if (Value == 0) throw new InvalidOperationException("Empty bitboard");
            int lsb = BitOperations.TrailingZeroCount(Value);
            ulong v = Value & (Value - 1);
            // not mutable; return square only; caller should manage their own ulong
            return new Square((byte)lsb);
        }

        public bool Equals(Bitboard other) => Value == other.Value;
        public override bool Equals(object obj) => obj is Bitboard b && Equals(b);
        public override int GetHashCode() => Value.GetHashCode();
    }
}