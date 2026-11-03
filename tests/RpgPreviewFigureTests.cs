namespace Cthangover.Tools.RpgObjectEditor.Tests
{
    using Cthangover.Core.Mods.Testing;
    using Godot;

    public class RpgPreviewFigureTests
    {
        [ModTest("preview/character_size_is_bound_to_cells_and_feet")]
        static void Scale(ModTestContext context)
        {
            var anchor = new Vector2(0.5f, 1);
            var small = RpgPreviewFigure.Bounds(new Vector2(40, 80), new Rect2I(8, 10, 24, 60), anchor);
            var large = RpgPreviewFigure.Bounds(new Vector2(160, 320), new Rect2I(32, 40, 96, 240), anchor);
            ModAssert.True(small.IsEqualApprox(large), "4x larger PNG produces the same cell size and foot position");
            ModAssert.CloseTo(1.8, small.Size.Y * 60 / 80, what: "visible body height in cells");
            ModAssert.CloseTo(1, small.Position.Y + small.Size.Y * 70 / 80, what: "visible feet match the ground anchor");
        }
        [ModTest("preview/real_jrp_and_rpgplus_figures_have_equal_visible_height")]
        static void RealSheets(ModTestContext context)
        {
            foreach (var path in new[] { "res://mods/junior_reaper_paper_grief/charsets/jrp/stas.png", "res://mods/village_pack/charsets/village/hero.png" })
            {
                using var image = Image.LoadFromFile(path);
                using var texture = ImageTexture.CreateFromImage(image);
                var look = new Cthangover.Mods.TileRender.TileVisual { Texture = texture, Anchor = new Vector2(0.5f, 1) };
                RpgPreviewFigure.Normalize(look);
                var frame = Cthangover.Mods.TileRender.TileWalkSheet.Frame(texture.GetSize(), 1, 0);
                var body = RpgPreviewFigure.LargestBody(image, new Rect2I((Vector2I)frame.Position, (Vector2I)frame.Size), 1);
                ModAssert.CloseTo(1.8, look.Bounds.Size.Y * body.Size.Y / frame.Size.Y, what: path + " body height in cells");
            }
        }

        [ModTest("preview/largest_alpha_component_ignores_stray_pixels")]
        static void Body(ModTestContext context)
        {
            using var image = Image.CreateEmpty(80, 80, false, Image.Format.Rgba8);
            image.Fill(Colors.Transparent);
            image.FillRect(new Rect2I(20, 10, 25, 50), Colors.White);
            image.SetPixel(1, 1, Colors.White);
            var bounds = RpgPreviewFigure.LargestBody(image, new Rect2I(0, 0, 80, 80), 1);
            ModAssert.True(bounds == new Rect2I(20, 10, 25, 50), "detached artefacts do not alter body size or foot point");
        }
    }
}
