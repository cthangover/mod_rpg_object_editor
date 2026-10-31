namespace Cthangover.Tools.RpgObjectEditor
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using System.Text.RegularExpressions;

    /// <summary>What a picture is used for: decides the size rule it is checked against.</summary>
    public enum RpgPictureRole
    {
        /// <summary>A part of a sprite: whole cells of the map's <c>TileSize</c>.</summary>
        Part,
        /// <summary>A segment (one cell wide fence piece): whole cells.</summary>
        Segment,
        /// <summary>A surface material: a square of the map's <c>PatchSize</c>.</summary>
        Surface,
    }

    /// <summary>
    /// Checks a PNG chosen for import (plan 12.3, coordinator decision 7): the file must be a PNG, its size
    /// must fit the grid of the maps. Problems stop the import; grid warnings are shown and the import goes on.
    /// Messages are locale keys: the caller formats them with the picture's width, height, tile and patch size.
    /// Without Godot, so the rules are plain C#.
    /// </summary>
    public static class RpgPictureCheck
    {
        /// <summary>Default pixels per cell of a map (<c>TileSize</c>).</summary>
        public const int DefaultTileSize = 32;

        /// <summary>Default side of a surface patch (<c>PatchSize</c>).</summary>
        public const int DefaultPatchSize = 128;

        private static readonly byte[] Signature = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };
        private static readonly Regex Clean = new("[^a-z0-9_\\-]", RegexOptions.CultureInvariant);

        /// <summary>The outcome of a check.</summary>
        public sealed class Result
        {
            /// <summary>Width in pixels (0 when the file is not a PNG).</summary>
            public int Width { get; init; }

            /// <summary>Height in pixels (0 when the file is not a PNG).</summary>
            public int Height { get; init; }

            /// <summary>Locale keys of the problems that stop the import.</summary>
            public List<string> Problems { get; } = new();

            /// <summary>Locale keys of the grid warnings (the import still happens).</summary>
            public List<string> Warnings { get; } = new();

            /// <summary>True when nothing stops the import.</summary>
            public bool Ok => Problems.Count == 0;
        }

        /// <summary>Checks the bytes of a PNG file for <paramref name="role"/> on maps with the given sizes.</summary>
        public static Result Check(byte[] bytes, RpgPictureRole role, int tileSize = DefaultTileSize, int patchSize = DefaultPatchSize)
        {
            if (!TryReadSize(bytes, out var width, out var height))
            {
                var failed = new Result();
                failed.Problems.Add("tools/rpg_object_editor/import_not_png");
                return failed;
            }

            var result = new Result { Width = width, Height = height };
            if (width <= 0 || height <= 0)
            {
                result.Problems.Add("tools/rpg_object_editor/import_empty");
                return result;
            }
            if (role == RpgPictureRole.Surface)
            {
                if (width != height || width != patchSize)
                    result.Warnings.Add("tools/rpg_object_editor/import_surface_size");
                return result;
            }
            if (tileSize > 0 && (width % tileSize != 0 || height % tileSize != 0))
                result.Warnings.Add("tools/rpg_object_editor/import_grid");
            return result;
        }

        /// <summary>
        /// Reads width and height from the IHDR chunk of a PNG. False when the bytes are not a PNG
        /// (signature and first chunk).
        /// </summary>
        public static bool TryReadSize(byte[] bytes, out int width, out int height)
        {
            width = 0;
            height = 0;
            if (bytes == null || bytes.Length < 24)
                return false;
            for (var i = 0; i < Signature.Length; i++)
            {
                if (bytes[i] != Signature[i])
                    return false;
            }
            if (Encoding.ASCII.GetString(bytes, 12, 4) != "IHDR")
                return false;
            width = ReadBigEndian(bytes, 16);
            height = ReadBigEndian(bytes, 20);
            return true;
        }

        /// <summary>
        /// The id a picture gets in <c>rpg_images</c> from its category and file name: lower-case letters, digits,
        /// <c>_</c> and <c>-</c>. Null when nothing is left of either.
        /// </summary>
        public static string TargetId(string category, string fileName)
        {
            var folder = Clean.Replace(Spaced(category), string.Empty);
            var name = Clean.Replace(Spaced(fileName), string.Empty);
            if (folder.Length == 0 || name.Length == 0)
                return null;
            return folder + "/" + name;
        }

        private static string Spaced(string text) => (text ?? string.Empty).Trim().ToLowerInvariant().Replace(' ', '_');

        private static int ReadBigEndian(byte[] bytes, int offset)
            => (bytes[offset] << 24) | (bytes[offset + 1] << 16) | (bytes[offset + 2] << 8) | bytes[offset + 3];
    }
}
