using System;
using System.Collections.Generic;
using UnityEngine;

namespace LightingScenarioTool
{
    internal static class ColorPresetStore
    {
        private const string PlayerPrefsKey = "LightingScenarioTool.ColorPresets.v1";
        private const int MaxPresetCount = 12;

        public static IReadOnlyList<Color> Load()
        {
            var result = new List<Color>();
            var raw = PlayerPrefs.GetString(PlayerPrefsKey, string.Empty);
            if (string.IsNullOrWhiteSpace(raw)) return result;

            var tokens = raw.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
            for (var i = 0; i < tokens.Length && result.Count < MaxPresetCount; i++)
            {
                if (ColorUtility.TryParseHtmlString(tokens[i], out var color))
                    result.Add(new Color(color.r, color.g, color.b, 1f));
            }
            return result;
        }

        public static void Add(Color color)
        {
            color.a = 1f;
            var colors = new List<Color>(Load());

            // Avoid duplicate presets. If the same color already exists, move it to the front.
            for (var i = colors.Count - 1; i >= 0; i--)
            {
                if (ApproximatelySameRgb(colors[i], color))
                    colors.RemoveAt(i);
            }

            colors.Insert(0, color);
            if (colors.Count > MaxPresetCount)
                colors.RemoveRange(MaxPresetCount, colors.Count - MaxPresetCount);

            Save(colors);
        }

        public static void RemoveAt(int index)
        {
            var colors = new List<Color>(Load());
            if (index < 0 || index >= colors.Count) return;

            colors.RemoveAt(index);
            Save(colors);
        }

        private static void Save(IReadOnlyList<Color> colors)
        {
            var encoded = new List<string>(colors != null ? colors.Count : 0);
            if (colors != null)
            {
                for (var i = 0; i < colors.Count && i < MaxPresetCount; i++)
                    encoded.Add("#" + ColorUtility.ToHtmlStringRGB(colors[i]));
            }

            PlayerPrefs.SetString(PlayerPrefsKey, string.Join(";", encoded));
            PlayerPrefs.Save();
        }

        private static bool ApproximatelySameRgb(Color a, Color b)
        {
            const float tolerance = 0.5f / 255f;
            return Mathf.Abs(a.r - b.r) <= tolerance &&
                   Mathf.Abs(a.g - b.g) <= tolerance &&
                   Mathf.Abs(a.b - b.b) <= tolerance;
        }
    }
}
