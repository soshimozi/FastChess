using System;

namespace UnityChess.Library
{
    public readonly struct BoardState : IEquatable<BoardState>
    {
        // Bitboards indexed by Piece enum (0 unused); 1..12
        private readonly ulong _wp, _wn, _wb, _wr, _wq, _wk;
        private readonly ulong _bp, _bn, _bb, _br, _bq, _bk;

        public readonly Color SideToMove;
        public readonly byte Castling; // bits: 0 WK,1 WQ,2 BK,3 BQ
        private readonly byte _enPassant; // 0..63 or 255 for none
        public readonly ushort HalfmoveClock;
        public readonly ushort FullmoveNumber;
        public readonly ulong ZobristKey;

        public BoardState(
            ulong wp, ulong wn, ulong wb, ulong wr, ulong wq, ulong wk,
            ulong bp, ulong bn, ulong bb, ulong br, ulong bq, ulong bk,
            Color stm, byte castling, byte enPassant, ushort halfmove, ushort fullmove, ulong zobrist)
        {
            _wp = wp; _wn = wn; _wb = wb; _wr = wr; _wq = wq; _wk = wk;
            _bp = bp; _bn = bn; _bb = bb; _br = br; _bq = bq; _bk = bk;
            SideToMove = stm;
            Castling = castling;
            _enPassant = enPassant;
            HalfmoveClock = halfmove;
            FullmoveNumber = fullmove;
            ZobristKey = zobrist;
        }

        public ulong WhiteOcc => _wp | _wn | _wb | _wr | _wq | _wk;
        public ulong BlackOcc => _bp | _bn | _bb | _br | _bq | _bk;
        public ulong Occ => WhiteOcc | BlackOcc;

        public ulong Pieces(Piece p) => p switch
        {
            Piece.WP => _wp, Piece.WN => _wn, Piece.WB => _wb, Piece.WR => _wr, Piece.WQ => _wq, Piece.WK => _wk,
            Piece.BP => _bp, Piece.BN => _bn, Piece.BB => _bb, Piece.BR => _br, Piece.BQ => _bq, Piece.BK => _bk,
            _ => 0UL
        };

        public ulong Pieces(Color c, PieceType t)
        {
            return (c, t) switch
            {
                (Color.White, PieceType.Pawn) => _wp,
                (Color.White, PieceType.Knight) => _wn,
                (Color.White, PieceType.Bishop) => _wb,
                (Color.White, PieceType.Rook) => _wr,
                (Color.White, PieceType.Queen) => _wq,
                (Color.White, PieceType.King) => _wk,

                (Color.Black, PieceType.Pawn) => _bp,
                (Color.Black, PieceType.Knight) => _bn,
                (Color.Black, PieceType.Bishop) => _bb,
                (Color.Black, PieceType.Rook) => _br,
                (Color.Black, PieceType.Queen) => _bq,
                (Color.Black, PieceType.King) => _bk,
                _ => 0UL
            };
        }

        public Piece PieceAt(Square sq)
        {
            ulong m = 1UL << sq.Index;
            if ((_wp & m) != 0) return Piece.WP;
            if ((_wn & m) != 0) return Piece.WN;
            if ((_wb & m) != 0) return Piece.WB;
            if ((_wr & m) != 0) return Piece.WR;
            if ((_wq & m) != 0) return Piece.WQ;
            if ((_wk & m) != 0) return Piece.WK;

            if ((_bp & m) != 0) return Piece.BP;
            if ((_bn & m) != 0) return Piece.BN;
            if ((_bb & m) != 0) return Piece.BB;
            if ((_br & m) != 0) return Piece.BR;
            if ((_bq & m) != 0) return Piece.BQ;
            if ((_bk & m) != 0) return Piece.BK;

            return Piece.None;
        }

        public Square KingSquare(Color c)
        {
            ulong k = c == Color.White ? _wk : _bk;
            if (k == 0) throw new InvalidOperationException("No king on board");
            int idx = System.Numerics.BitOperations.TrailingZeroCount(k);
            return new Square((byte)idx);
        }

        public bool InCheck(Color side)
        {
            Square ksq = KingSquare(side);
            return IsSquareAttacked(ksq, side.Opp());
        }

        public bool IsSquareAttacked(Square sq, Color by)
        {
            ulong occ = Occ;
            int s = sq.Index;

            // Pawns
            if ((Attacks.Pawn[(int)by.Opp(), s] & Pieces(by, PieceType.Pawn)) != 0) return true;
            // Knights
            if ((Attacks.Knight[s] & Pieces(by, PieceType.Knight)) != 0) return true;
            // King
            if ((Attacks.King[s] & Pieces(by, PieceType.King)) != 0) return true;

            // Bishops / Queens
            ulong bishopsQueens = Pieces(by, PieceType.Bishop) | Pieces(by, PieceType.Queen);
            if ((Attacks.Bishop(s, occ) & bishopsQueens) != 0) return true;

            // Rooks / Queens
            ulong rooksQueens = Pieces(by, PieceType.Rook) | Pieces(by, PieceType.Queen);
            if ((Attacks.Rook(s, occ) & rooksQueens) != 0) return true;

            return false;
        }

        // Immutable make move: returns a new BoardState
        public BoardState MakeMove(Move mv)
        {
            // This is the core hook; full rules: captures, ep, castling, promo, clocks, zobrist.
            // Implemented as a correct-but-straightforward version you can optimize later.

            var from = mv.From;
            var to = mv.To;
            var moving = PieceAt(from);
            if (moving == Piece.None) throw new InvalidOperationException("No piece at from-square");
            if (moving.ColorOf() != SideToMove) throw new InvalidOperationException("Wrong side to move");

            var captured = PieceAt(to);

            // Clear from; clear captured; set to; handle special flags
            ulong fromMask = 1UL << from.Index;
            ulong toMask = 1UL << to.Index;

            ulong wp = _wp, wn = _wn, wb = _wb, wr = _wr, wq = _wq, wk = _wk;
            ulong bp = _bp, bn = _bn, bb = _bb, br = _br, bq = _bq, bk = _bk;

            // remove moving piece from from-square
            RemovePiece(ref wp, ref wn, ref wb, ref wr, ref wq, ref wk, ref bp, ref bn, ref bb, ref br, ref bq, ref bk, moving, fromMask);

            // remove captured piece from to-square
            if (captured != Piece.None)
                RemovePiece(ref wp, ref wn, ref wb, ref wr, ref wq, ref wk, ref bp, ref bn, ref bb, ref br, ref bq, ref bk, captured, toMask);

            byte newCastling = Castling;
            byte newEp = 255;
            ushort newHalfmove = (ushort)(captured != Piece.None || moving.TypeOf() == PieceType.Pawn ? 0 : HalfmoveClock + 1);
            ushort newFullmove = (ushort)(SideToMove == Color.Black ? FullmoveNumber + 1 : FullmoveNumber);

            // Special: en passant capture
            if ((mv.Flags & MoveFlags.EnPassant) != 0)
            {
                int epCapSq = SideToMove == Color.White ? to.Index - 8 : to.Index + 8;
                ulong epMask = 1UL << epCapSq;
                var epPawn = SideToMove == Color.White ? Piece.BP : Piece.WP;
                RemovePiece(ref wp, ref wn, ref wb, ref wr, ref wq, ref wk, ref bp, ref bn, ref bb, ref br, ref bq, ref bk, epPawn, epMask);
                newHalfmove = 0;
            }

            // Special: castling
            if ((mv.Flags & MoveFlags.Castle) != 0)
            {
                // King moved to g/c; rook shifts accordingly.
                // White: e1->g1 rook h1->f1; e1->c1 rook a1->d1
                // Black: e8->g8 rook h8->f8; e8->c8 rook a8->d8
                ApplyCastleRookMove(SideToMove, to, ref wr, ref br);
                // Clear castling rights for side
                newCastling = ClearCastlingForSide(newCastling, SideToMove);
            }

            // Special: promotion
            Piece placed = moving;
            if ((mv.Flags & MoveFlags.Promotion) != 0)
            {
                var promo = mv.Promotion ?? throw new InvalidOperationException("Promotion flag but no piece");
                placed = PieceExt.Make(SideToMove, promo);
                newHalfmove = 0;
            }

            // Pawn double push sets EP square
            if (moving.TypeOf() == PieceType.Pawn)
            {
                int diff = to.Index - from.Index;
                if (diff == 16 || diff == -16)
                {
                    int epSq = (from.Index + to.Index) / 2;
                    newEp = (byte)epSq;
                }
            }

            // Place moving piece (or promoted piece) on to-square
            AddPiece(ref wp, ref wn, ref wb, ref wr, ref wq, ref wk, ref bp, ref bn, ref bb, ref br, ref bq, ref bk, placed, toMask);

            // Update castling rights if king or rooks move or are captured
            newCastling = UpdateCastlingForRookKingMoves(newCastling, moving, from, captured, to);

            // Zobrist incremental update
            ulong z = ZobristKey;

            // Remove old EP file (if any)
            if (_enPassant != 255)
                z ^= Zobrist.EpFileKeyForSquare(_enPassant);

            // Remove old castling key; add new later
            z ^= Zobrist.CastlingRightsKey(Castling);

            // Flip side-to-move
            z ^= Zobrist.SideKey();

            // Moving piece: XOR out from-square
            z ^= Zobrist.PieceKey(moving, from.Index);

            // Captures
            if (captured != Piece.None)
            {
                z ^= Zobrist.PieceKey(captured, to.Index);
            }

            // En passant capture removes pawn behind target
            if ((mv.Flags & MoveFlags.EnPassant) != 0)
            {
                int epCapSq = SideToMove == Color.White ? to.Index - 8 : to.Index + 8;
                var epPawn = SideToMove == Color.White ? Piece.BP : Piece.WP;
                z ^= Zobrist.PieceKey(epPawn, epCapSq);
            }

            // Castling rook movement affects hash too
            if ((mv.Flags & MoveFlags.Castle) != 0)
            {
                if (SideToMove == Color.White)
                {
                    // King: e1->g1 rook h1->f1 OR e1->c1 rook a1->d1
                    if (to.Index == 6)
                    {
                        z ^= Zobrist.PieceKey(Piece.WR, 7); // out h1
                        z ^= Zobrist.PieceKey(Piece.WR, 5); // in  f1
                    }
                    else if (to.Index == 2)
                    {
                        z ^= Zobrist.PieceKey(Piece.WR, 0); // out a1
                        z ^= Zobrist.PieceKey(Piece.WR, 3); // in  d1
                    }
                }
                else
                {
                    // e8->g8 rook h8->f8 OR e8->c8 rook a8->d8
                    if (to.Index == 62)
                    {
                        z ^= Zobrist.PieceKey(Piece.BR, 63); // out h8
                        z ^= Zobrist.PieceKey(Piece.BR, 61); // in  f8
                    }
                    else if (to.Index == 58)
                    {
                        z ^= Zobrist.PieceKey(Piece.BR, 56); // out a8
                        z ^= Zobrist.PieceKey(Piece.BR, 59); // in  d8
                    }
                }
            }

            // Promotion changes what lands on the destination
            Piece landed = placed;
            z ^= Zobrist.PieceKey(landed, to.Index);

            // Add new castling key
            z ^= Zobrist.CastlingRightsKey(newCastling);

            // Add new EP file (if any)
            if (newEp != 255)
                z ^= Zobrist.EpFileKeyForSquare(newEp);

            ulong newZ = z;

            return new BoardState(wp, wn, wb, wr, wq, wk, bp, bn, bb, br, bq, bk,
                SideToMove.Opp(), newCastling, newEp, newHalfmove, newFullmove, newZ);
        }

        public bool IsDrawByInsufficientMaterial() => DrawRules.IsInsufficientMaterial(in this);

        private static void RemovePiece(
            ref ulong wp, ref ulong wn, ref ulong wb, ref ulong wr, ref ulong wq, ref ulong wk,
            ref ulong bp, ref ulong bn, ref ulong bb, ref ulong br, ref ulong bq, ref ulong bk,
            Piece p, ulong mask)
        {
            switch (p)
            {
                case Piece.WP: wp &= ~mask; break;
                case Piece.WN: wn &= ~mask; break;
                case Piece.WB: wb &= ~mask; break;
                case Piece.WR: wr &= ~mask; break;
                case Piece.WQ: wq &= ~mask; break;
                case Piece.WK: wk &= ~mask; break;

                case Piece.BP: bp &= ~mask; break;
                case Piece.BN: bn &= ~mask; break;
                case Piece.BB: bb &= ~mask; break;
                case Piece.BR: br &= ~mask; break;
                case Piece.BQ: bq &= ~mask; break;
                case Piece.BK: bk &= ~mask; break;
            }
        }

        private static void AddPiece(
            ref ulong wp, ref ulong wn, ref ulong wb, ref ulong wr, ref ulong wq, ref ulong wk,
            ref ulong bp, ref ulong bn, ref ulong bb, ref ulong br, ref ulong bq, ref ulong bk,
            Piece p, ulong mask)
        {
            switch (p)
            {
                case Piece.WP: wp |= mask; break;
                case Piece.WN: wn |= mask; break;
                case Piece.WB: wb |= mask; break;
                case Piece.WR: wr |= mask; break;
                case Piece.WQ: wq |= mask; break;
                case Piece.WK: wk |= mask; break;

                case Piece.BP: bp |= mask; break;
                case Piece.BN: bn |= mask; break;
                case Piece.BB: bb |= mask; break;
                case Piece.BR: br |= mask; break;
                case Piece.BQ: bq |= mask; break;
                case Piece.BK: bk |= mask; break;
            }
        }

        private static byte ClearCastlingForSide(byte c, Color side)
        {
            return side == Color.White ? (byte)(c & ~0b0011) : (byte)(c & ~0b1100);
        }

        private static void ApplyCastleRookMove(Color side, Square kingTo, ref ulong whiteRooks, ref ulong blackRooks)
        {
            if (side == Color.White)
            {
                // kingTo g1 (6) or c1 (2)
                if (kingTo.Index == 6)
                {
                    // h1 -> f1
                    whiteRooks &= ~(1UL << 7);
                    whiteRooks |= (1UL << 5);
                }
                else if (kingTo.Index == 2)
                {
                    // a1 -> d1
                    whiteRooks &= ~(1UL << 0);
                    whiteRooks |= (1UL << 3);
                }
            }
            else
            {
                // g8 (62) or c8 (58)
                if (kingTo.Index == 62)
                {
                    // h8 -> f8
                    blackRooks &= ~(1UL << 63);
                    blackRooks |= (1UL << 61);
                }
                else if (kingTo.Index == 58)
                {
                    // a8 -> d8
                    blackRooks &= ~(1UL << 56);
                    blackRooks |= (1UL << 59);
                }
            }
        }

        private static byte UpdateCastlingForRookKingMoves(byte c, Piece moving, Square from, Piece captured, Square to)
        {
            // King moves clear both rights for that side
            if (moving == Piece.WK) c = (byte)(c & ~0b0011);
            if (moving == Piece.BK) c = (byte)(c & ~0b1100);

            // Rook moves from corner squares clear that side’s right
            if (moving == Piece.WR)
            {
                if (from.Index == 0) c = (byte)(c & ~0b0010);
                if (from.Index == 7) c = (byte)(c & ~0b0001);
            }
            if (moving == Piece.BR)
            {
                if (from.Index == 56) c = (byte)(c & ~0b1000);
                if (from.Index == 63) c = (byte)(c & ~0b0100);
            }

            // Capturing a rook on its corner clears that side’s right
            if (captured == Piece.WR)
            {
                if (to.Index == 0) c = (byte)(c & ~0b0010);
                if (to.Index == 7) c = (byte)(c & ~0b0001);
            }
            if (captured == Piece.BR)
            {
                if (to.Index == 56) c = (byte)(c & ~0b1000);
                if (to.Index == 63) c = (byte)(c & ~0b0100);
            }

            return c;
        }

        public bool Equals(BoardState other)
        {
            return _wp == other._wp && _wn == other._wn && _wb == other._wb && _wr == other._wr && _wq == other._wq && _wk == other._wk
                && _bp == other._bp && _bn == other._bn && _bb == other._bb && _br == other._br && _bq == other._bq && _bk == other._bk
                && SideToMove == other.SideToMove && Castling == other.Castling && _enPassant == other._enPassant
                && HalfmoveClock == other.HalfmoveClock && FullmoveNumber == other.FullmoveNumber
                && ZobristKey == other.ZobristKey;
        }

        public override bool Equals(object obj) => obj is BoardState s && Equals(s);
        public override int GetHashCode() {
            HashCode hash = new();
            hash.Add(_wp); hash.Add(_wn); hash.Add(_wb); hash.Add(_wr); hash.Add(_wq); hash.Add(_wk);
            hash.Add(_bp); hash.Add(_bn); hash.Add(_bb); hash.Add(_br); hash.Add(_bq); hash.Add(_bk);
            hash.Add(SideToMove); hash.Add(Castling); hash.Add(_enPassant);
            hash.Add(HalfmoveClock); hash.Add(FullmoveNumber); hash.Add(ZobristKey);
            return hash.ToHashCode();
        }

        public BoardState WithZobrist(ulong zobrist)
        {
            return new BoardState(_wp, _wn, _wb, _wr, _wq, _wk, _bp, _bn, _bb, _br, _bq, _bk,
                SideToMove, Castling, _enPassant, HalfmoveClock, FullmoveNumber, zobrist);
        }        

        // Rust-ish: en_passant() -> Option<Square>
        public Square? EnPassant()
        {
            return _enPassant == 255 ? (Square?)null : new Square(_enPassant);
        }

        // Rust-ish: combined() -> BitBoard
        public ulong Combined() => Occ;

        // Rust-ish: color_combined(color) -> BitBoard
        public ulong ColorCombined(Color c) => c == Color.White ? WhiteOcc : BlackOcc;

        // Rust-ish: piece_on(square) -> Option<Piece>
        public Piece PieceOn(Square sq) => PieceAt(sq);

        // Rust-ish: color_on(square) -> Option<Color>
        public Color? ColorOn(Square sq)
        {
            var p = PieceAt(sq);
            return p == Piece.None ? (Color?)null : p.ColorOf();
        }

        public CastlingRights MyCastlingRights()
        {
            var c = (CastlingRights)(Castling & 0x0F);
            return SideToMove == Color.White
                ? (c & (CastlingRights.WhiteKingSide | CastlingRights.WhiteQueenSide))
                : (c & (CastlingRights.BlackKingSide | CastlingRights.BlackQueenSide));
        }

        public CastlingRights TheirCastlingRights()
        {
            var c = (CastlingRights)(Castling & 0x0F);
            return SideToMove == Color.White
                ? (c & (CastlingRights.BlackKingSide | CastlingRights.BlackQueenSide))
                : (c & (CastlingRights.WhiteKingSide | CastlingRights.WhiteQueenSide));
        }

        // Rust-ish: status() -> Ongoing/Stalemate/Checkmate
        public GameStatus Status()
        {
            // If you use the fast generator, prefer it here.
            var moves = ThreadLocalMoveList.Rent();
            moves.Clear();
            LegalMoveGenFast.GenerateLegal(in this, moves);

            bool hasMoves = moves.Count != 0;
            bool inCheck = InCheck(SideToMove);

            if (hasMoves) return GameStatus.Ongoing;
            if (inCheck) return GameStatus.Checkmate;
            return GameStatus.Stalemate;
        }

        // Rust-ish: is_legal(move) -> bool
        public bool IsLegal(Move mv)
        {
            // Cheapest correct approach: generate legal moves and match.
            // If you want faster, we can add a direct "validate move" path later.
            var moves = ThreadLocalMoveList.Rent();
            moves.Clear();
            LegalMoveGenFast.GenerateLegal(in this, moves);

            for (int i = 0; i < moves.Count; i++)
            {
                if (moves[i].Equals(mv)) return true;
            }
            return false;
        }

        // Rust-ish: pinned() for side-to-move
        public ulong Pinned()
        {
            AnalyzeForSideToMove(out var pinned, out _);
            return pinned;
        }

        // Rust-ish: checkers() for side-to-move
        public ulong Checkers()
        {
            AnalyzeForSideToMove(out _, out var checkers);
            return checkers;
        }

        // ---------- Internal analysis ----------

        private void AnalyzeForSideToMove(out ulong pinned, out ulong checkers)
        {
            Color us = SideToMove;
            Color them = us.Opp();

            ulong usOcc = us == Color.White ? WhiteOcc : BlackOcc;
            ulong themOcc = us == Color.White ? BlackOcc : WhiteOcc;
            ulong occ = usOcc | themOcc;

            int ksq = KingSquare(us).Index;

            pinned = 0;
            checkers = 0;

            // Knight checkers
            ulong theirKnights = Pieces(them, PieceType.Knight);
            checkers |= Attacks.Knight[ksq] & theirKnights;

            // Pawn checkers: squares from which opponent pawns attack our king
            ulong theirPawns = Pieces(them, PieceType.Pawn);
            checkers |= Attacks.Pawn[(int)us, ksq] & theirPawns;

            // Slider checkers and pins
            ulong theirRQ = Pieces(them, PieceType.Rook) | Pieces(them, PieceType.Queen);
            ulong theirBQ = Pieces(them, PieceType.Bishop) | Pieces(them, PieceType.Queen);

            // Walk 8 directions from the king.
            WalkDirForPinsAndChecks(ksq, +8, usOcc, occ, theirRQ, ref pinned, ref checkers);
            WalkDirForPinsAndChecks(ksq, -8, usOcc, occ, theirRQ, ref pinned, ref checkers);
            WalkDirForPinsAndChecks(ksq, +1, usOcc, occ, theirRQ, ref pinned, ref checkers);
            WalkDirForPinsAndChecks(ksq, -1, usOcc, occ, theirRQ, ref pinned, ref checkers);

            WalkDirForPinsAndChecks(ksq, +9, usOcc, occ, theirBQ, ref pinned, ref checkers);
            WalkDirForPinsAndChecks(ksq, +7, usOcc, occ, theirBQ, ref pinned, ref checkers);
            WalkDirForPinsAndChecks(ksq, -7, usOcc, occ, theirBQ, ref pinned, ref checkers);
            WalkDirForPinsAndChecks(ksq, -9, usOcc, occ, theirBQ, ref pinned, ref checkers);
        }

        private void WalkDirForPinsAndChecks(
            int kingSq, int delta, ulong usOcc, ulong occ, ulong theirSliders,
            ref ulong pinned, ref ulong checkers)
        {
            int from = kingSq;
            int prevFile = from & 7;

            int firstOwnSq = -1;

            while (true)
            {
                int next = from + delta;
                if ((uint)next > 63) break;

                int nextFile = next & 7;
                int df = nextFile - prevFile;

                // prevent wrap across files for rank and diagonal steps
                if (delta == +1 || delta == -1)
                {
                    if (Math.Abs(df) != 1) break;
                }
                else if (delta == +9 || delta == -9 || delta == +7 || delta == -7)
                {
                    if (Math.Abs(df) != 1) break;
                }

                ulong bit = 1UL << next;
                if ((occ & bit) == 0)
                {
                    from = next;
                    prevFile = nextFile;
                    continue;
                }

                // occupied
                if ((usOcc & bit) != 0)
                {
                    if (firstOwnSq == -1)
                    {
                        firstOwnSq = next;
                        from = next;
                        prevFile = nextFile;
                        continue;
                    }
                    break; // second own piece blocks everything
                }
                else
                {
                    // enemy piece
                    if ((theirSliders & bit) != 0)
                    {
                        if (firstOwnSq == -1)
                        {
                            checkers |= bit; // direct check
                        }
                        else
                        {
                            pinned |= 1UL << firstOwnSq; // pinned piece found
                        }
                    }
                    break;
                }
            }
        }

        // Thread-local scratch MoveList to avoid allocations in Status/IsLegal
        private static class ThreadLocalMoveList
        {
            [ThreadStatic] private static MoveList _moves;
            public static MoveList Rent()
            {
                _moves ??= new MoveList();
                return _moves;
            }
        }   

        public static BoardState Default()
        {
            // Uses the same canonical startpos string as Rust chess crate behavior.
            return Fen.Default();
        }             
    }
}