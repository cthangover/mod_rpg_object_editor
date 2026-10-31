namespace Cthangover.Tools.RpgObjectEditor
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Text.Json.Nodes;

    /// <summary>
    /// Numbers typed in the object form (<c>"1.5, -2"</c>) and stored as JSON arrays. The point is always the
    /// decimal sign and the text is read with the invariant culture, so a player's language does not change
    /// what an object means (plan 13.0). Without Godot, so it is tested as plain C#.
    /// </summary>
    public static class RpgObjectNumbers
    {
        /// <summary>
        /// Reads numbers separated by commas, semicolons or spaces. Empty text gives null (the property goes);
        /// false when a part is not a number.
        /// </summary>
        public static bool TryParse(string text, out float[] values)
        {
            values = null;
            var parts = (text ?? string.Empty).Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0)
                return true;
            var result = new float[parts.Length];
            for (var i = 0; i < parts.Length; i++)
            {
                if (!float.TryParse(parts[i], NumberStyles.Float, CultureInfo.InvariantCulture, out result[i]))
                    return false;
            }
            values = result;
            return true;
        }

        /// <summary>The numbers as the form shows them: <c>"1, 2.5"</c>; null or empty gives an empty text.</summary>
        public static string Format(IEnumerable<float> values)
            => values == null ? string.Empty : string.Join(", ", values.Select(v => v.ToString("0.###", CultureInfo.InvariantCulture)));

        /// <summary>The numbers as a JSON array (null stays null).</summary>
        public static JsonArray ToNode(IEnumerable<float> values)
            => values == null ? null : new JsonArray(values.Select(v => (JsonNode)JsonValue.Create((double)v)).ToArray());

        /// <summary>Whole numbers as a JSON array (crops, pixel sizes); null stays null.</summary>
        public static JsonArray ToIntNode(IEnumerable<int> values)
            => values == null ? null : new JsonArray(values.Select(v => (JsonNode)JsonValue.Create(v)).ToArray());

        /// <summary>The numbers of a JSON array; null when the node is not an array of numbers.</summary>
        public static float[] FromNode(JsonNode node)
        {
            if (node is not JsonArray array)
                return null;
            var result = new float[array.Count];
            for (var i = 0; i < array.Count; i++)
            {
                // Whole numbers written in this session are int nodes, which do not read as double: both are accepted.
                if (array[i] is not JsonValue value)
                    return null;
                if (value.TryGetValue(out double number))
                    result[i] = (float)number;
                else if (value.TryGetValue(out int whole))
                    result[i] = whole;
                else
                    return null;
            }
            return result;
        }
    }
}
