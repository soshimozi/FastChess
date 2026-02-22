using System;

namespace UnityChess.Library
{
    public readonly struct Square : IEquatable<Square>
    {
        public readonly byte Index; // 0..63; a1 = 0, b1 = 1 ... h8 = 63

        public Square(byte index)
        {
            if (index > 63) throw new ArgumentOutOfRangeException(nameof(index));
            Index = index;
        }

        public static implicit operator byte(Square square) => square.Index;

        public int File => Index & 7;     // 0..7
        public int Rank => Index >> 3;    // 0..7

        public override string ToString()
        {
            char f = (char)('a' + File);
            char r = (char)('1' + Rank);
            return new string(new[] { f, r });
        }

        public bool Equals(Square other) => Index == other.Index;
        public override bool Equals(object? obj) => obj is Square s && Equals(s);
        public override int GetHashCode() => Index;

        public static bool TryParse(string s, out Square sq)
        {
            sq = default;
            if (string.IsNullOrEmpty(s) || s.Length != 2) return false;

            char f = s[0];
            char r = s[1];
            if (f < 'a' || f > 'h') return false;
            if (r < '1' || r > '8') return false;

            int file = f - 'a';
            int rank = r - '1';
            sq = new Square((byte)(rank * 8 + file));
            return true;
        }

        public static Square FromFileRank(int file, int rank)
        {
            if ((uint)file > 7 || (uint)rank > 7) throw new ArgumentOutOfRangeException();
            return new Square((byte)(rank * 8 + file));
        }

        // Rank 1
        public static readonly Square A1 = new Square(0);
        public static readonly Square B1 = new Square(1);
        public static readonly Square C1 = new Square(2);
        public static readonly Square D1 = new Square(3);
        public static readonly Square E1 = new Square(4);
        public static readonly Square F1 = new Square(5);
        public static readonly Square G1 = new Square(6);
        public static readonly Square H1 = new Square(7);

        // Rank 2
        public static readonly Square A2 = new Square(8);
        public static readonly Square B2 = new Square(9);
        public static readonly Square C2 = new Square(10);
        public static readonly Square D2 = new Square(11);
        public static readonly Square E2 = new Square(12);
        public static readonly Square F2 = new Square(13);
        public static readonly Square G2 = new Square(14);
        public static readonly Square H2 = new Square(15);

        // Rank 3
        public static readonly Square A3 = new Square(16);
        public static readonly Square B3 = new Square(17);
        public static readonly Square C3 = new Square(18);
        public static readonly Square D3 = new Square(19);
        public static readonly Square E3 = new Square(20);
        public static readonly Square F3 = new Square(21);
        public static readonly Square G3 = new Square(22);
        public static readonly Square H3 = new Square(23);

        // Rank 4
        public static readonly Square A4 = new Square(24);
        public static readonly Square B4 = new Square(25);
        public static readonly Square C4 = new Square(26);
        public static readonly Square D4 = new Square(27);
        public static readonly Square E4 = new Square(28);
        public static readonly Square F4 = new Square(29);
        public static readonly Square G4 = new Square(30);
        public static readonly Square H4 = new Square(31);

        // Rank 5
        public static readonly Square A5 = new Square(32);
        public static readonly Square B5 = new Square(33);
        public static readonly Square C5 = new Square(34);
        public static readonly Square D5 = new Square(35);
        public static readonly Square E5 = new Square(36);
        public static readonly Square F5 = new Square(37);
        public static readonly Square G5 = new Square(38);
        public static readonly Square H5 = new Square(39);

        // Rank 6
        public static readonly Square A6 = new Square(40);
        public static readonly Square B6 = new Square(41);
        public static readonly Square C6 = new Square(42);
        public static readonly Square D6 = new Square(43);
        public static readonly Square E6 = new Square(44);
        public static readonly Square F6 = new Square(45);
        public static readonly Square G6 = new Square(46);
        public static readonly Square H6 = new Square(47);

        // Rank 7
        public static readonly Square A7 = new Square(48);
        public static readonly Square B7 = new Square(49);
        public static readonly Square C7 = new Square(50);
        public static readonly Square D7 = new Square(51);
        public static readonly Square E7 = new Square(52);
        public static readonly Square F7 = new Square(53);
        public static readonly Square G7 = new Square(54);
        public static readonly Square H7 = new Square(55);

        // Rank 8
        public static readonly Square A8 = new Square(56);
        public static readonly Square B8 = new Square(57);
        public static readonly Square C8 = new Square(58);
        public static readonly Square D8 = new Square(59);
        public static readonly Square E8 = new Square(60);
        public static readonly Square F8 = new Square(61);
        public static readonly Square G8 = new Square(62);
        public static readonly Square H8 = new Square(63);

    }
}
