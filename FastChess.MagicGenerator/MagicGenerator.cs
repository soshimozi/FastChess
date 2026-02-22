using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;

namespace FastChess.Core
{
    public static class MagicGenerator
    {
        // Deterministic RNG; fixed seed.
        private struct XorShift64
        {
            private ulong _s;
            public XorShift64(ulong seed) => _s = seed == 0 ? 0x9E3779B97F4A7C15UL : seed;
            public ulong Next()
            {
                ulong x = _s;
                x ^= x << 13;
                x ^= x >> 7;
                x ^= x << 17;
                _s = x;
                return x;
            }
            public ulong NextSparse()
            {
                // common trick: AND 3 randoms to get sparse bits
                return Next() & Next() & Next();
            }
        }

        public static string GenerateAndLog()
        {
            var rook = BuildSlider(isRook: true, seed: 0xC0FFEEUL);
            var bishop = BuildSlider(isRook: false, seed: 0xBADC0DEUL);

            string cs = EmitCSharp(rook, bishop);
            return cs;
        }

        private sealed class SliderData
        {
            public ulong[] Mask = new ulong[64];
            public ulong[] Magic = new ulong[64];
            public byte[] Shift = new byte[64];
            public int[] Offset = new int[64];
            public ulong[] Table = Array.Empty<ulong>();
        }

        private static SliderData BuildSlider(bool isRook, ulong seed)
        {
            var rng = new XorShift64(seed);
            var sd = new SliderData();

            // Build masks and shifts; allocate offsets based on bits.
            int total = 0;
            for (int sq = 0; sq < 64; sq++)
            {
                sd.Mask[sq] = isRook ? RookMask(sq) : BishopMask(sq);
                int bits = BitOperations.PopCount(sd.Mask[sq]);
                sd.Shift[sq] = (byte)(64 - bits);
                sd.Offset[sq] = total;
                total += 1 << bits;
            }

            sd.Table = new ulong[total];

            // For each square, find magic and fill table
            for (int sq = 0; sq < 64; sq++)
            {
                int bits = 64 - sd.Shift[sq];
                int size = 1 << bits;

                var occs = new ulong[size];
                var atts = new ulong[size];
                BuildOccupanciesAndAttacks(sq, sd.Mask[sq], isRook, occs, atts);

                ulong magic = FindMagicForSquare(sq, sd.Mask[sq], sd.Shift[sq], occs, atts, ref rng);
                sd.Magic[sq] = magic;

                // Fill attack table for this square
                int baseOff = sd.Offset[sq];
                Array.Fill(sd.Table, 0UL, baseOff, size);

                for (int i = 0; i < size; i++)
                {
                    ulong occ = occs[i];
                    ulong idx = (occ * magic) >> sd.Shift[sq];
                    sd.Table[baseOff + (int)idx] = atts[i];
                }
            }

            return sd;
        }

        private static ulong FindMagicForSquare(int sq, ulong mask, byte shift, ulong[] occs, ulong[] atts, ref XorShift64 rng)
        {
            int size = occs.Length;
            var used = new ulong[size];

            for (int attempt = 0; attempt < 100_000_000; attempt++)
            {
                ulong magic = rng.NextSparse();

                // quick reject heuristic used in many implementations
                ulong test = (mask * magic) & 0xFF00000000000000UL;
                if (BitOperations.PopCount(test) < 6) continue;

                Array.Clear(used, 0, used.Length);
                bool fail = false;

                for (int i = 0; i < size; i++)
                {
                    ulong idx = (occs[i] * magic) >> shift;
                    ref ulong slot = ref used[(int)idx];

                    if (slot == 0UL)
                    {
                        slot = atts[i];
                    }
                    else if (slot != atts[i])
                    {
                        fail = true;
                        break;
                    }
                }

                if (!fail) return magic;
            }

            throw new InvalidOperationException($"Failed to find magic for square {sq}");
        }

        private static void BuildOccupanciesAndAttacks(int sq, ulong mask, bool isRook, ulong[] occs, ulong[] atts)
        {
            int bits = BitOperations.PopCount(mask);
            int size = 1 << bits;

            Span<int> bitPos = stackalloc int[bits];
            int k = 0;
            ulong tmp = mask;
            while (tmp != 0)
            {
                int b = BitOperations.TrailingZeroCount(tmp);
                tmp &= tmp - 1;
                bitPos[k++] = b;
            }

            for (int i = 0; i < size; i++)
            {
                ulong occ = 0;
                for (int j = 0; j < bits; j++)
                {
                    if (((i >> j) & 1) != 0) occ |= 1UL << bitPos[j];
                }
                occs[i] = occ;
                atts[i] = isRook ? RookAttacksOnTheFly(sq, occ) : BishopAttacksOnTheFly(sq, occ);
            }
        }

        private static ulong RookMask(int sq)
        {
            int f = sq & 7;
            int r = sq >> 3;
            ulong m = 0;

            // exclude edge squares
            for (int rr = r + 1; rr <= 6; rr++) m |= 1UL << (rr * 8 + f);
            for (int rr = r - 1; rr >= 1; rr--) m |= 1UL << (rr * 8 + f);
            for (int ff = f + 1; ff <= 6; ff++) m |= 1UL << (r * 8 + ff);
            for (int ff = f - 1; ff >= 1; ff--) m |= 1UL << (r * 8 + ff);

            return m;
        }

        private static ulong BishopMask(int sq)
        {
            int f = sq & 7;
            int r = sq >> 3;
            ulong m = 0;

            // exclude edge squares
            for (int ff = f + 1, rr = r + 1; ff <= 6 && rr <= 6; ff++, rr++) m |= 1UL << (rr * 8 + ff);
            for (int ff = f - 1, rr = r + 1; ff >= 1 && rr <= 6; ff--, rr++) m |= 1UL << (rr * 8 + ff);
            for (int ff = f + 1, rr = r - 1; ff <= 6 && rr >= 1; ff++, rr--) m |= 1UL << (rr * 8 + ff);
            for (int ff = f - 1, rr = r - 1; ff >= 1 && rr >= 1; ff--, rr--) m |= 1UL << (rr * 8 + ff);

            return m;
        }

        private static ulong RookAttacksOnTheFly(int sq, ulong occ)
        {
            int f = sq & 7;
            int r = sq >> 3;
            ulong a = 0;

            for (int rr = r + 1; rr <= 7; rr++)
            {
                int s = rr * 8 + f;
                a |= 1UL << s;
                if (((occ >> s) & 1) != 0) break;
            }
            for (int rr = r - 1; rr >= 0; rr--)
            {
                int s = rr * 8 + f;
                a |= 1UL << s;
                if (((occ >> s) & 1) != 0) break;
            }
            for (int ff = f + 1; ff <= 7; ff++)
            {
                int s = r * 8 + ff;
                a |= 1UL << s;
                if (((occ >> s) & 1) != 0) break;
            }
            for (int ff = f - 1; ff >= 0; ff--)
            {
                int s = r * 8 + ff;
                a |= 1UL << s;
                if (((occ >> s) & 1) != 0) break;
            }

            return a;
        }

        private static ulong BishopAttacksOnTheFly(int sq, ulong occ)
        {
            int f = sq & 7;
            int r = sq >> 3;
            ulong a = 0;

            for (int ff = f + 1, rr = r + 1; ff <= 7 && rr <= 7; ff++, rr++)
            {
                int s = rr * 8 + ff;
                a |= 1UL << s;
                if (((occ >> s) & 1) != 0) break;
            }
            for (int ff = f - 1, rr = r + 1; ff >= 0 && rr <= 7; ff--, rr++)
            {
                int s = rr * 8 + ff;
                a |= 1UL << s;
                if (((occ >> s) & 1) != 0) break;
            }
            for (int ff = f + 1, rr = r - 1; ff <= 7 && rr >= 0; ff++, rr--)
            {
                int s = rr * 8 + ff;
                a |= 1UL << s;
                if (((occ >> s) & 1) != 0) break;
            }
            for (int ff = f - 1, rr = r - 1; ff >= 0 && rr >= 0; ff--, rr--)
            {
                int s = rr * 8 + ff;
                a |= 1UL << s;
                if (((occ >> s) & 1) != 0) break;
            }

            return a;
        }

        private static string EmitCSharp(SliderData rook, SliderData bishop)
        {
            var sb = new StringBuilder(1_000_000);
            sb.AppendLine("namespace FastChess.Core");
            sb.AppendLine("{");
            sb.AppendLine("    // Generated by MagicGenerator; paste into MagicData.cs");
            sb.AppendLine("    public static class MagicData");
            sb.AppendLine("    {");
            EmitArray(sb, "public static readonly ulong[] RookMask", rook.Mask);
            EmitArray(sb, "public static readonly ulong[] BishopMask", bishop.Mask);
            EmitArray(sb, "public static readonly ulong[] RookMagic", rook.Magic);
            EmitArray(sb, "public static readonly ulong[] BishopMagic", bishop.Magic);
            EmitArray(sb, "public static readonly byte[] RookShift", rook.Shift);
            EmitArray(sb, "public static readonly byte[] BishopShift", bishop.Shift);
            EmitArray(sb, "public static readonly int[] RookOffset", rook.Offset);
            EmitArray(sb, "public static readonly int[] BishopOffset", bishop.Offset);
            EmitArray(sb, "public static readonly ulong[] RookTable", rook.Table);
            EmitArray(sb, "public static readonly ulong[] BishopTable", bishop.Table);
            sb.AppendLine("    }");
            sb.AppendLine("}");
            return sb.ToString();
        }

        private static void EmitArray(StringBuilder sb, string decl, ulong[] arr)
        {
            sb.Append(decl).AppendLine(" = new ulong[]");
            sb.AppendLine("        {");
            for (int i = 0; i < arr.Length; i++)
            {
                sb.Append("            0x").Append(arr[i].ToString("X16")).Append("UL,");
                if ((i & 3) == 3) sb.AppendLine();
            }
            sb.AppendLine();
            sb.AppendLine("        };");
            sb.AppendLine();
        }

        private static void EmitArray(StringBuilder sb, string decl, byte[] arr)
        {
            sb.Append(decl).AppendLine(" = new byte[]");
            sb.AppendLine("        {");
            for (int i = 0; i < arr.Length; i++)
            {
                sb.Append("            ").Append(arr[i]).Append(",");
                if ((i & 15) == 15) sb.AppendLine();
            }
            sb.AppendLine();
            sb.AppendLine("        };");
            sb.AppendLine();
        }

        private static void EmitArray(StringBuilder sb, string decl, int[] arr)
        {
            sb.Append(decl).AppendLine(" = new int[]");
            sb.AppendLine("        {");
            for (int i = 0; i < arr.Length; i++)
            {
                sb.Append("            ").Append(arr[i]).Append(",");
                if ((i & 15) == 15) sb.AppendLine();
            }
            sb.AppendLine();
            sb.AppendLine("        };");
            sb.AppendLine();
        }
    }
}
