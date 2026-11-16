using UnityEngine;

namespace DefenseBattle.View
{
    /// <summary>원본 CSS 색 (rgba(...), #rrggbb) → Color32</summary>
    public static class Rgba
    {
        public static Color32 Of(int r, int g, int b, double a) => new Color32((byte)r, (byte)g, (byte)b, A(a));

        /// <summary>0xRRGGBB + 알파</summary>
        public static Color32 Hex(uint rgb, double a = 1) => new Color32((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb, A(a));

        public static Color32 WithAlpha(Color32 c, double a) => new Color32(c.r, c.g, c.b, A(a));

        static byte A(double a) => (byte)Mathf.RoundToInt(Mathf.Clamp01((float)a) * 255f);
    }
}
