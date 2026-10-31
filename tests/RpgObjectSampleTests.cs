namespace Cthangover.Tools.RpgObjectEditor.Tests
{
    using System.Linq;
    using System.Text.Json.Nodes;
    using Cthangover.Core.Mods.Testing;
    using Cthangover.Mods.Rpg.Model;

    /// <summary>The editor's sample maps and the object it previews as the game reads it (no Godot).</summary>
    public class RpgObjectSampleTests
    {
        private static RpgObject Object(RpgObjectForm form, string id = "m/o") => new() { ID = id, Form = form };

        private static JsonObject Json(string text) => JsonNode.Parse(text).AsObject();

        [ModTest("sample/surface_is_a_five_by_five_patch")]
        static void SurfaceIsAFiveByFivePatch(ModTestContext ctx)
        {
            var surface = Object(RpgObjectForm.Surface, "m/ground/grass");
            var cells = RpgObjectSample.Cells(surface);
            ModAssert.Equal(5, cells.Width, "width");
            ModAssert.Equal(5, cells.Height, "height");
            ModAssert.Equal("m/ground/grass", cells.Ground(2, 2)?.ID, "the centre cell is the material");
            ModAssert.Equal("m/ground/grass", cells.Ground(4, 4)?.ID, "the last cell is the material");
            ModAssert.Null(cells.Structure(2, 2), "a surface sample has no segments");
        }

        [ModTest("sample/segment_is_a_cross_of_segments")]
        static void SegmentIsACross(ModTestContext ctx)
        {
            var segment = Object(RpgObjectForm.Segment, "m/fence");
            var cells = RpgObjectSample.Cells(segment);
            ModAssert.Equal(3, cells.Height, "height");
            ModAssert.Equal("m/fence", cells.Structure(2, 0)?.ID, "the top arm");
            ModAssert.Equal("m/fence", cells.Structure(0, 1)?.ID, "the left arm of the row");
            ModAssert.Equal("m/fence", cells.Structure(2, 2)?.ID, "the bottom arm");
            ModAssert.Null(cells.Structure(0, 0), "a corner stays empty");
            ModAssert.Null(cells.Ground(2, 1), "a segment sample has no ground");
        }

        [ModTest("sample/sprite_stands_on_the_field")]
        static void SpriteStandsOnTheField(ModTestContext ctx)
        {
            var sprite = Object(RpgObjectForm.Sprite, "m/tree");
            var statics = RpgObjectSample.Statics(sprite).ToList();
            ModAssert.Equal(1, statics.Count, "one placed object");
            ModAssert.Equal(RpgObjectSample.SpriteX, statics[0].X, "x");
            ModAssert.Equal(RpgObjectSample.SpriteY, statics[0].Y, "y");
            ModAssert.Equal(7, RpgObjectSample.Map(sprite).Width, "field width");
            ModAssert.Equal(0, RpgObjectSample.Statics(Object(RpgObjectForm.Actor)).Count(), "an actor is drawn as a pose, not placed");
        }

        [ModTest("sample/effective_merges_the_parent_of_an_own_object")]
        static void EffectiveMergesParent(ModTestContext ctx)
        {
            var parent = Json("{\"Id\": \"p/a\", \"Form\": \"sprite\", \"Category\": \"tree\", \"Parts\": [{\"Id\": \"main\", \"Image\": \"p/tree\"}]}");
            var child = Json("{\"Id\": \"c/b\", \"Form\": \"sprite\", \"Extends\": \"p/a\", \"Category\": \"bush\"}");
            var effective = RpgObjectSample.Effective(child, id => id == "p/a" ? parent : null, resolved: false);
            ModAssert.Equal("bush", effective.Category, "the child's own value wins");
            ModAssert.Equal(1, effective.Parts.Count, "the parent's part is inherited");
            ModAssert.Equal("p/tree", effective.Parts[0].Image, "its picture");
        }

        [ModTest("sample/effective_takes_a_resolved_foreign_row_as_it_is")]
        static void EffectiveKeepsResolved(ModTestContext ctx)
        {
            var parent = Json("{\"Id\": \"p/a\", \"Form\": \"sprite\", \"Parts\": [{\"Id\": \"main\", \"Image\": \"p/tree\"}]}");
            var resolved = Json("{\"Id\": \"c/b\", \"Form\": \"sprite\", \"Extends\": \"p/a\", \"Parts\": [{\"Id\": \"main\", \"Image\": \"c/only\"}]}");
            var effective = RpgObjectSample.Effective(resolved, id => parent, resolved: true);
            ModAssert.Equal(1, effective.Parts.Count, "no second merge: the part is not doubled");
            ModAssert.Equal("c/only", effective.Parts[0].Image, "the row's own resolved picture");
        }

        [ModTest("sample/effective_leaves_variants_out")]
        static void EffectiveLeavesVariantsOut(ModTestContext ctx)
        {
            var node = Json("{\"Id\": \"v/c\", \"Form\": \"sprite\", \"Variants\": {\"winter\": {\"Category\": \"snow\"}}}");
            var effective = RpgObjectSample.Effective(node, null, resolved: true);
            ModAssert.Null(effective.Variants, "the default variant has no variant layer left");
        }
    }
}
