using UnityEngine;

namespace Playlio.Auth
{
    /// <summary>Neon UI colours from the Playlio auth starter (ui-design.md).</summary>
    public static class PlaylioNeonPalette
    {
        public static readonly Color Canvas = Hex("#05070D");
        public static readonly Color Base = Hex("#080B14");
        public static readonly Color Raised = Hex("#0D1220");
        public static readonly Color Elevated = Hex("#11182A");
        public static readonly Color Cyan = Hex("#00D9E8");
        public static readonly Color CyanSoft = Hex("#66F7FF");
        public static readonly Color Violet = Hex("#8B5CF6");
        public static readonly Color Magenta = Hex("#F52FCF");
        public static readonly Color Lime = Hex("#9EF01A");
        public static readonly Color Orange = Hex("#FF8A1F");
        public static readonly Color Red = Hex("#FF375F");
        public static readonly Color TextPrimary = Hex("#F7FAFF");
        public static readonly Color TextSecondary = Hex("#B7C1D6");
        public static readonly Color Border = Hex("#1A2440");

        private static Color Hex(string value)
        {
            ColorUtility.TryParseHtmlString(value, out var color);
            return color;
        }
    }
}
