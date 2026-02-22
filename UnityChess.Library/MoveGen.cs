using System;
using System.Numerics;

namespace UnityChess.Library
{
    public static class MoveGen
    {
        public static void GeneratePseudoLegal(in BoardState s, MoveList list)
        {
            list.Clear();
            ulong us = s.SideToMove == Color.White ? s.WhiteOcc : s.BlackOcc;
            ulong them = s.SideToMove == Color.White ? s.BlackOcc : s.WhiteOcc;
            ulong occ = us | them;

            GenKnights(in s, list, us, them);
            GenBishopsRooksQueens(in s, list, us, them, occ);
            GenKing(in s, list, us, them);

            GenPawns(in s, list, us, them, occ);
        }

        public static void GenerateLegal(in BoardState s, MoveList list)
        {
            GeneratePseudoLegal(in s, list);

            // Filter in place by copying to temp.
            // You can micro-opt later by writing to a second list.
            var tmp = new MoveList();
            for (int i = 0; i < list.Count; i++)
            {
                var mv = list[i];
                var ns = s.MakeMove(mv);
                if (!ns.InCheck(s.SideToMove)) tmp.Add(mv);
            }

            list.Clear();
            for (int i = 0; i < tmp.Count; i++) list.Add(tmp[i]);
        }

        private static void GenKnights(in BoardState s, MoveList list, ulong us, ulong them)
        {
            ulong knights = s.Pieces(s.SideToMove, PieceType.Knight);
            while (knights != 0)
            {
                int from = BitOperations.TrailingZeroCount(knights);
                ulong fromMask = 1UL << from;
                knights &= knights - 1;

                ulong attacks = Attacks.Knight[from] & ~us;
                while (attacks != 0)
                {
                    int to = BitOperations.TrailingZeroCount(attacks);
                    attacks &= attacks - 1;

                    var flags = ((them >> to) & 1) != 0 ? MoveFlags.Capture : MoveFlags.None;
                    list.Add(Move.Create(new Square((byte)from), new Square((byte)to), flags));
                }
            }
        }

        private static void GenKing(in BoardState s, MoveList list, ulong us, ulong them)
        {
            ulong king = s.Pieces(s.SideToMove, PieceType.King);
            int from = BitOperations.TrailingZeroCount(king);

            ulong attacks = Attacks.King[from] & ~us;
            while (attacks != 0)
            {
                int to = BitOperations.TrailingZeroCount(attacks);
                attacks &= attacks - 1;

                var flags = ((them >> to) & 1) != 0 ? MoveFlags.Capture : MoveFlags.None;
                list.Add(Move.Create(new Square((byte)from), new Square((byte)to), flags));
            }

            // Castling generation plugs in here.
            // Castling (pseudo-legal but checks empty + attacked squares properly)
            if (s.SideToMove == Color.White)
            {
                int e1 = 4, f1 = 5, g1 = 6, d1 = 3, c1 = 2, b1 = 1;
                if ((s.Castling & 0b0001) != 0) // K
                {
                    ulong occ = s.Occ;
                    if (((occ >> f1) & 1) == 0 && ((occ >> g1) & 1) == 0)
                    {
                        // e1, f1, g1 must not be attacked by black
                        if (!s.IsSquareAttacked(new Square((byte)e1), Color.Black) &&
                            !s.IsSquareAttacked(new Square((byte)f1), Color.Black) &&
                            !s.IsSquareAttacked(new Square((byte)g1), Color.Black))
                        {
                            list.Add(Move.Create(new Square((byte)e1), new Square((byte)g1), MoveFlags.Castle));
                        }
                    }
                }
                if ((s.Castling & 0b0010) != 0) // Q
                {
                    ulong occ = s.Occ;
                    if (((occ >> d1) & 1) == 0 && ((occ >> c1) & 1) == 0 && ((occ >> b1) & 1) == 0)
                    {
                        if (!s.IsSquareAttacked(new Square((byte)e1), Color.Black) &&
                            !s.IsSquareAttacked(new Square((byte)d1), Color.Black) &&
                            !s.IsSquareAttacked(new Square((byte)c1), Color.Black))
                        {
                            list.Add(Move.Create(new Square((byte)e1), new Square((byte)c1), MoveFlags.Castle));
                        }
                    }
                }
            }
            else
            {
                int e8 = 60, f8 = 61, g8 = 62, d8 = 59, c8 = 58, b8 = 57;
                if ((s.Castling & 0b0100) != 0) // k
                {
                    ulong occ = s.Occ;
                    if (((occ >> f8) & 1) == 0 && ((occ >> g8) & 1) == 0)
                    {
                        if (!s.IsSquareAttacked(new Square((byte)e8), Color.White) &&
                            !s.IsSquareAttacked(new Square((byte)f8), Color.White) &&
                            !s.IsSquareAttacked(new Square((byte)g8), Color.White))
                        {
                            list.Add(Move.Create(new Square((byte)e8), new Square((byte)g8), MoveFlags.Castle));
                        }
                    }
                }
                if ((s.Castling & 0b1000) != 0) // q
                {
                    ulong occ = s.Occ;
                    if (((occ >> d8) & 1) == 0 && ((occ >> c8) & 1) == 0 && ((occ >> b8) & 1) == 0)
                    {
                        if (!s.IsSquareAttacked(new Square((byte)e8), Color.White) &&
                            !s.IsSquareAttacked(new Square((byte)d8), Color.White) &&
                            !s.IsSquareAttacked(new Square((byte)c8), Color.White))
                        {
                            list.Add(Move.Create(new Square((byte)e8), new Square((byte)c8), MoveFlags.Castle));
                        }
                    }
                }
            }            
       }

        private static void GenBishopsRooksQueens(in BoardState s, MoveList list, ulong us, ulong them, ulong occ)
        {
            GenSliders(in s, list, us, them, occ, PieceType.Bishop);
            GenSliders(in s, list, us, them, occ, PieceType.Rook);
            GenSliders(in s, list, us, them, occ, PieceType.Queen);
        }

        private static void GenSliders(in BoardState s, MoveList list, ulong us, ulong them, ulong occ, PieceType pt)
        {
            ulong bb = s.Pieces(s.SideToMove, pt);

            while (bb != 0)
            {
                int from = BitOperations.TrailingZeroCount(bb);
                bb &= bb - 1;

                ulong attacks = pt switch
                {
                    PieceType.Bishop => Attacks.Bishop(from, occ),
                    PieceType.Rook => Attacks.Rook(from, occ),
                    PieceType.Queen => Attacks.Queen(from, occ),
                    _ => 0
                };

                attacks &= ~us;
                while (attacks != 0)
                {
                    int to = BitOperations.TrailingZeroCount(attacks);
                    attacks &= attacks - 1;

                    var flags = ((them >> to) & 1) != 0 ? MoveFlags.Capture : MoveFlags.None;
                    list.Add(Move.Create(new Square((byte)from), new Square((byte)to), flags));
                }
            }
        }

        private static void GenPawns(in BoardState s, MoveList list, ulong us, ulong them, ulong occ)
        {
            // This is a correct starter for single and double pushes and captures, excluding promos and EP.
            bool white = s.SideToMove == Color.White;
            ulong pawns = s.Pieces(s.SideToMove, PieceType.Pawn);
            ulong empty = ~occ;

            if (white)
            {
                // Single pushes
                ulong one = (pawns << 8) & empty;
                AddPawnPushes(list, one, -8, promoRank: 7);

                // Double pushes from rank 2
                ulong rank2 = 0x000000000000FF00UL;
                ulong two = ((pawns & rank2) << 16) & empty & (empty << 8);
                AddPawnDoublePushes(list, two, -16);

                // Captures
                ulong capL = ((pawns & ~0x0101010101010101UL) << 7) & them;
                ulong capR = ((pawns & ~0x8080808080808080UL) << 9) & them;
                AddPawnCaptures(list, capL, -7, promoRank: 7);
                AddPawnCaptures(list, capR, -9, promoRank: 7);

                // En passant plugs in here.
            }
            else
            {
                ulong one = (pawns >> 8) & empty;
                AddPawnPushes(list, one, +8, promoRank: 0);

                ulong rank7 = 0x00FF000000000000UL;
                ulong two = ((pawns & rank7) >> 16) & empty & (empty >> 8);
                AddPawnDoublePushes(list, two, +16);

                ulong capL = ((pawns & ~0x0101010101010101UL) >> 9) & them;
                ulong capR = ((pawns & ~0x8080808080808080UL) >> 7) & them;
                AddPawnCaptures(list, capL, +9, promoRank: 0);
                AddPawnCaptures(list, capR, +7, promoRank: 0);

                // En passant plugs in here.
            }

            // En passant (pseudo-legal)
            if (s.EnPassant()?.Index != 255)
            {
                int epSq = s.EnPassant()?.Index ?? 0;
                ulong epMask = 1UL << epSq;

                if (white)
                {
                    // Capturing pawn must be on rank 5; ep target is on rank 6
                    // Pawn from epSq-9 (capture right from pawn perspective) or epSq-7
                    // Ensure file boundaries
                    if ((epMask & 0xFF00000000000000UL) == 0) // ep cannot be on rank 8 anyway; cheap guard
                    {
                        // From left pawn (captures right): from = epSq - 9, requires pawn exists and not file A
                        if ((epSq % 8) != 0)
                        {
                            int from = epSq - 9;
                            if (from >= 0 && ((s.Pieces(Color.White, PieceType.Pawn) >> from) & 1) != 0)
                                list.Add(Move.Create(new Square((byte)from), new Square((byte)epSq), MoveFlags.EnPassant | MoveFlags.Capture));
                        }
                        // From right pawn (captures left): from = epSq - 7, requires pawn exists and not file H
                        if ((epSq % 8) != 7)
                        {
                            int from = epSq - 7;
                            if (from >= 0 && ((s.Pieces(Color.White, PieceType.Pawn) >> from) & 1) != 0)
                                list.Add(Move.Create(new Square((byte)from), new Square((byte)epSq), MoveFlags.EnPassant | MoveFlags.Capture));
                        }
                    }
                }
                else
                {
                    // Black ep target is on rank 3
                    if ((epSq % 8) != 0)
                    {
                        int from = epSq + 7;
                        if (from <= 63 && ((s.Pieces(Color.Black, PieceType.Pawn) >> from) & 1) != 0)
                            list.Add(Move.Create(new Square((byte)from), new Square((byte)epSq), MoveFlags.EnPassant | MoveFlags.Capture));
                    }
                    if ((epSq % 8) != 7)
                    {
                        int from = epSq + 9;
                        if (from <= 63 && ((s.Pieces(Color.Black, PieceType.Pawn) >> from) & 1) != 0)
                            list.Add(Move.Create(new Square((byte)from), new Square((byte)epSq), MoveFlags.EnPassant | MoveFlags.Capture));
                    }
                }
            }


        }

        private static void AddPawnPushes(MoveList list, ulong targets, int fromDelta, int promoRank)
        {
            while (targets != 0)
            {
                int to = BitOperations.TrailingZeroCount(targets);
                targets &= targets - 1;

                int from = to + fromDelta;
                int toRank = to >> 3;

                if (toRank == promoRank)
                {
                    list.Add(Move.Create(new Square((byte)from), new Square((byte)to), MoveFlags.Promotion, PieceType.Queen));
                    list.Add(Move.Create(new Square((byte)from), new Square((byte)to), MoveFlags.Promotion, PieceType.Rook));
                    list.Add(Move.Create(new Square((byte)from), new Square((byte)to), MoveFlags.Promotion, PieceType.Bishop));
                    list.Add(Move.Create(new Square((byte)from), new Square((byte)to), MoveFlags.Promotion, PieceType.Knight));
                }
                else
                {
                    list.Add(Move.Create(new Square((byte)from), new Square((byte)to)));
                }
            }
        }

        private static void AddPawnDoublePushes(MoveList list, ulong targets, int fromDelta)
        {
            while (targets != 0)
            {
                int to = BitOperations.TrailingZeroCount(targets);
                targets &= targets - 1;

                int from = to + fromDelta;
                list.Add(Move.Create(new Square((byte)from), new Square((byte)to)));
            }
        }

        private static void AddPawnCaptures(MoveList list, ulong targets, int fromDelta, int promoRank)
        {
            while (targets != 0)
            {
                int to = BitOperations.TrailingZeroCount(targets);
                targets &= targets - 1;

                int from = to + fromDelta;
                int toRank = to >> 3;

                if (toRank == promoRank)
                {
                    list.Add(Move.Create(new Square((byte)from), new Square((byte)to), MoveFlags.Capture | MoveFlags.Promotion, PieceType.Queen));
                    list.Add(Move.Create(new Square((byte)from), new Square((byte)to), MoveFlags.Capture | MoveFlags.Promotion, PieceType.Rook));
                    list.Add(Move.Create(new Square((byte)from), new Square((byte)to), MoveFlags.Capture | MoveFlags.Promotion, PieceType.Bishop));
                    list.Add(Move.Create(new Square((byte)from), new Square((byte)to), MoveFlags.Capture | MoveFlags.Promotion, PieceType.Knight));
                }
                else
                {
                    list.Add(Move.Create(new Square((byte)from), new Square((byte)to), MoveFlags.Capture));
                }
            }
        }
    }
}