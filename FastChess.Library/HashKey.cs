using System;

namespace FastChess.Library
{
    public readonly struct HashKey : IEquatable<HashKey>
    {
        public static readonly HashKey Empty = new HashKey(0UL);

        public readonly ulong Value;

        public HashKey(ulong value)
        {
            Value = value;
        }

        public static HashKey operator ^(HashKey a, HashKey b) => new HashKey(a.Value ^ b.Value);
        public static HashKey operator ^(HashKey a, ulong b) => new HashKey(a.Value ^ b);

        public static implicit operator ulong(HashKey key) => key.Value;
        public static implicit operator HashKey(ulong value) => new HashKey(value);

        public bool Equals(HashKey other) => Value == other.Value;
        public override bool Equals(object? obj) => obj is HashKey other && Equals(other);
        public override int GetHashCode() => Value.GetHashCode();

        public override string ToString() => $"0x{Value:X16}";
    }
}
