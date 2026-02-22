namespace UnityChess.Library;

    public enum Color : byte { White = 0, Black = 1 }

    public static class ColorExt
    {
        public static Color Opp(this Color c) => c == Color.White ? Color.Black : Color.White;
    }