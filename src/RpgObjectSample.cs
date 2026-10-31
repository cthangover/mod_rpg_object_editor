namespace Cthangover.Tools.RpgObjectEditor
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.Json;
    using System.Text.Json.Nodes;
    using Cthangover.Core.Mods.Patches;
    using Cthangover.Core.Utils;
    using Cthangover.Mods.Rpg.Model;

    /// <summary>
    /// What the object editor shows, without Godot: the object as the game reads it, and the small map
    /// it is drawn on. The preview hands the map to the game's own <c>RpgRenderSceneBuilder</c>; nothing
    /// here draws and nothing is saved or entered.
    /// </summary>
    public static class RpgObjectSample
    {
        /// <summary>Id of the sample map (never a real map).</summary>
        public const string MapId = "rpg_object_editor_sample";

        /// <summary>Position of an actor's pose on its sample map, in cells.</summary>
        public const int ActorX = 3;

        /// <summary>Position of an actor's pose on its sample map, in cells.</summary>
        public const int ActorY = 3;

        /// <summary>Top-left cell of a sprite on its sample map.</summary>
        public const int SpriteX = 2;

        /// <summary>Top-left cell of a sprite on its sample map.</summary>
        public const int SpriteY = 2;

        private const char Glyph = 'o';

        /// <summary>
        /// The object as the game reads it from <paramref name="data"/>. An own entity (<paramref name="resolved"/>
        /// false) is merged over its <c>Extends</c> parent, whose resolved value <paramref name="resolvedParent"/>
        /// gives; a foreign row is already resolved by the editor. Variants are left out (the default variant).
        /// Throws when the JSON does not read as an <see cref="RpgObject"/>.
        /// </summary>
        public static RpgObject Effective(JsonObject data, Func<string, JsonObject> resolvedParent, bool resolved)
            => RpgObjectResolve.Effective(Merged(data, resolvedParent, resolved), null);

        /// <summary>
        /// The object as read from <paramref name="data"/> with its <c>Extends</c> parent applied, variants and
        /// states kept (the preview picks one of them). Throws like <see cref="Effective"/>.
        /// </summary>
        public static RpgObject Merged(JsonObject data, Func<string, JsonObject> resolvedParent, bool resolved)
        {
            if (data == null)
                return null;
            var node = (JsonObject)data.DeepClone();
            var parentId = node["Extends"]?.ToString();
            if (!resolved && !string.IsNullOrEmpty(parentId))
            {
                var parent = resolvedParent?.Invoke(parentId);
                if (parent != null)
                {
                    var merged = (JsonObject)parent.DeepClone();
                    JsonPatchMerger.Apply(merged, node, typeof(RpgObject));
                    node = merged;
                }
            }
            return JsonSerializer.Deserialize<RpgObject>(node.ToJsonString(), JsonOptionsProvider.ReadOptions);
        }

        /// <summary>Glyph of the neighbouring surface in the sample's ground (the preview's "ground around").</summary>
        private const char Neighbour = 'n';

        /// <summary>Cell of the figure that stands behind the sprite (depth order: it is drawn under the sprite).</summary>
        public static readonly (int X, int Y) BehindCell = (SpriteX, SpriteY - 1);

        /// <summary>Cell of the figure that stands in front of the sprite (it is drawn over the sprite).</summary>
        public static readonly (int X, int Y) InFrontCell = (SpriteX + 1, SpriteY + 2);

        /// <summary>
        /// The sample map of <paramref name="sample"/>: a 5×5 patch of the surface, a 5×3 cross of segments,
        /// a 7×7 field with the sprite at <see cref="SpriteX"/>/<see cref="SpriteY"/> or the actor's pose.
        /// With a <paramref name="neighbour"/> surface the ground around the object is that material: a surface
        /// is then a 5×5 patch in a 7×7 ring of the neighbour (the transitions show), a segment and a sprite
        /// stand on the neighbour.
        /// </summary>
        public static RpgMap Map(RpgObject sample, string neighbour = null)
        {
            var id = sample?.ID ?? string.Empty;
            var map = new RpgMap { ID = MapId, Width = 7, Height = 7 };
            var ground = !string.IsNullOrEmpty(neighbour);
            if (ground)
                map.Legend = new Dictionary<string, string> { [Neighbour.ToString()] = neighbour };
            switch (sample?.Form)
            {
                case RpgObjectForm.Surface:
                    map.Legend = new Dictionary<string, string>(map.Legend ?? new Dictionary<string, string>()) { [Glyph.ToString()] = id };
                    if (ground)
                        map.Ground = Ring(7, 7, 1, Glyph, Neighbour);
                    else
                    {
                        map.Width = 5;
                        map.Height = 5;
                        map.Ground = Rows(5, 5, Glyph);
                    }
                    break;
                case RpgObjectForm.Segment:
                    map.Width = 5;
                    map.Height = 3;
                    map.Legend = new Dictionary<string, string>(map.Legend ?? new Dictionary<string, string>()) { [Glyph.ToString()] = id };
                    map.Structure = new List<string> { "..o..", "ooooo", "..o.." };
                    if (ground)
                        map.Ground = Rows(5, 3, Neighbour);
                    break;
                default:
                    if (ground)
                        map.Ground = Rows(7, 7, Neighbour);
                    break;
            }
            return map;
        }

        /// <summary>The grids of the sample map: the sample resolves to itself, the neighbour through <paramref name="objects"/>.</summary>
        public static RpgMapCells Cells(RpgObject sample, string neighbour = null, Func<string, RpgObject> objects = null)
            => RpgMapCells.Build(Map(sample, neighbour), id =>
            {
                if (sample != null && string.Equals(id, sample.ID ?? string.Empty, StringComparison.OrdinalIgnoreCase))
                    return sample;
                return objects?.Invoke(id);
            });

        /// <summary>The placed objects of the sample map: the sprite at its cell, nothing else.</summary>
        public static IEnumerable<(string Id, RpgObject Object, int X, int Y)> Statics(RpgObject sample)
        {
            if (sample?.Form == RpgObjectForm.Sprite)
                yield return (sample.ID, sample, SpriteX, SpriteY);
        }

        private static List<string> Rows(int width, int height, char glyph)
            => Enumerable.Repeat(new string(glyph, width), height).ToList();

        /// <summary>A field of <paramref name="inner"/> glyphs inside a <paramref name="border"/> wide ring of <paramref name="ring"/> glyphs.</summary>
        private static List<string> Ring(int width, int height, int border, char inner, char ring)
        {
            var rows = new List<string>();
            for (var y = 0; y < height; y++)
            {
                var onEdgeRow = y < border || y >= height - border;
                var text = new string(Enumerable.Range(0, width)
                    .Select(x => onEdgeRow || x < border || x >= width - border ? ring : inner).ToArray());
                rows.Add(text);
            }
            return rows;
        }
    }
}
