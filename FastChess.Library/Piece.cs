namespace FastChess.Library;

  public enum PieceType : byte { Pawn, Knight, Bishop, Rook, Queen, King }

  public enum Piece : byte
  {
      None = 0,
      WP = 1, WN, WB, WR, WQ, WK,
      BP = 7, BN, BB, BR, BQ, BK
  }

  public static class PieceExt
  {
      public static bool IsNone(this Piece p) => p == Piece.None;

      public static Color ColorOf(this Piece p)
      {
          return p <= Piece.WK ? Color.White : Color.Black;
      }

      public static PieceType TypeOf(this Piece p)
      {
          if (p == Piece.None) return PieceType.Pawn;
          int v = (int)p;
          if (v >= 7) v -= 6;
          return (PieceType)(v - 1);
      }

      public static Piece Make(Color c, PieceType t)
      {
          int baseVal = c == Color.White ? 1 : 7;
          return (Piece)(baseVal + (int)t);
      }
  }
