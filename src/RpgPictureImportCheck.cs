namespace Cthangover.Tools.RpgObjectEditor
{
    using System;
    using System.Text.Json.Nodes;
    using Cthangover.Core.Utils;
    using Cthangover.Mods.Rpg.Validation;
    using Godot;

    /// <summary>Import preflight shares role diagnostics with the old checker and structural material rules with map Save.</summary>
    public sealed class RpgPictureImportCheck
    {
        private readonly Vector2I size;

        private RpgPictureImportCheck(int width, int height)
        {
            size = new Vector2I(width, height);
        }

        /// <summary>
        /// Checks only the chosen file and role, allowing image replacement while other object fields need correction.
        /// Runtime additionally decodes the complete PNG into an owned temporary Image before any import copy is allowed.
        /// Square materials of another patch size remain import warnings; the using map supplies the required size on Save.
        /// </summary>
        public static RpgPictureCheck.Result Check(byte[] bytes, RpgPictureRole role)
        {
            var result = RpgPictureCheck.Check(bytes, role);
            if (!result.Ok)
            {
                return result;
            }

            if (bytes.Length < 45)
            {
                result.Problems.Add("tools/rpg_object_editor/import_not_png");
                return result;
            }

            if (GameEnvironment.IsRuntime)
            {
                using var image = new Image();
                if (image.LoadPngFromBuffer(bytes) != Error.Ok || image.IsEmpty())
                {
                    result.Problems.Add("tools/rpg_object_editor/import_not_png");
                    return result;
                }
            }

            if (role == RpgPictureRole.Surface)
            {
                var candidate = new JsonObject
                {
                    ["Form"] = "surface",
                    ["Surface"] = new JsonObject
                    {
                        ["Image"] = "candidate",
                    },
                };
                var check = new RpgPictureImportCheck(result.Width, result.Height);
                if (RpgDataPolicy.FirstBlocking(RpgDataCheck.Object(candidate, "topdown", 0, check.SizeOf)) != null)
                {
                    result.Problems.Add("tools/rpg_object_editor/import_surface_size");
                }
            }

            return result;
        }

        private Vector2I? SizeOf(string group, string id)
        {
            return size;
        }
    }
}
