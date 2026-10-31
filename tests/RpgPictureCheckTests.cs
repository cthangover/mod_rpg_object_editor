namespace Cthangover.Tools.RpgObjectEditor.Tests
{
    using System.Linq;
    using System.Text;
    using Cthangover.Core.Mods.Testing;

    /// <summary>The PNG import rules and the number text of the object form (no Godot).</summary>
    public class RpgPictureCheckTests
    {
        /// <summary>The first bytes of a PNG with the given size: signature and IHDR (the rest is not read).</summary>
        private static byte[] PngHeader(int width, int height)
        {
            var bytes = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 13 };
            var list = bytes.ToList();
            list.AddRange(Encoding.ASCII.GetBytes("IHDR"));
            list.AddRange(BigEndian(width));
            list.AddRange(BigEndian(height));
            list.AddRange(new byte[] { 8, 6, 0, 0, 0 });
            return list.ToArray();
        }

        private static byte[] BigEndian(int value)
            => new[] { (byte)(value >> 24), (byte)(value >> 16), (byte)(value >> 8), (byte)value };

        [ModTest("picture/a_png_size_is_read_from_its_header")]
        static void SizeIsRead(ModTestContext ctx)
        {
            ModAssert.True(RpgPictureCheck.TryReadSize(PngHeader(96, 64), out var width, out var height), "a PNG header reads");
            ModAssert.Equal(96, width, "width");
            ModAssert.Equal(64, height, "height");
        }

        [ModTest("picture/a_file_that_is_not_a_png_stops_the_import")]
        static void NotPngStops(ModTestContext ctx)
        {
            var text = Encoding.ASCII.GetBytes("this is not a picture at all, just text");
            var result = RpgPictureCheck.Check(text, RpgPictureRole.Part);
            ModAssert.False(result.Ok, "a text file is refused");
            ModAssert.Equal("tools/rpg_object_editor/import_not_png", result.Problems[0], "the problem key");
        }

        [ModTest("picture/a_part_on_the_cell_grid_is_clean")]
        static void GridClean(ModTestContext ctx)
        {
            var result = RpgPictureCheck.Check(PngHeader(64, 96), RpgPictureRole.Part);
            ModAssert.True(result.Ok, "64x96 px on a 32 px grid is allowed");
            ModAssert.Equal(0, result.Warnings.Count, "no grid warning");
        }

        [ModTest("picture/a_part_off_the_grid_only_warns")]
        static void GridWarns(ModTestContext ctx)
        {
            var result = RpgPictureCheck.Check(PngHeader(50, 40), RpgPictureRole.Part);
            ModAssert.True(result.Ok, "an off-grid picture still imports");
            ModAssert.Equal("tools/rpg_object_editor/import_grid", result.Warnings[0], "the grid warning");
        }

        [ModTest("picture/a_surface_must_be_a_square_patch")]
        static void SurfaceSquare(ModTestContext ctx)
        {
            var clean = RpgPictureCheck.Check(PngHeader(128, 128), RpgPictureRole.Surface);
            ModAssert.Equal(0, clean.Warnings.Count, "128x128 is the patch size");
            var warned = RpgPictureCheck.Check(PngHeader(128, 64), RpgPictureRole.Surface);
            ModAssert.Equal("tools/rpg_object_editor/import_surface_size", warned.Warnings[0], "a rectangle warns");
        }

        [ModTest("picture/target_id_is_folder_and_name_in_lower_case")]
        static void TargetId(ModTestContext ctx)
        {
            ModAssert.Equal("container/barrel_2", RpgPictureCheck.TargetId("Container", "Barrel 2!"), "cleaned id");
            ModAssert.Null(RpgPictureCheck.TargetId("misc", "!!!"), "nothing left of the name");
        }

        [ModTest("numbers/text_reads_point_decimals_whatever_the_culture")]
        static void NumbersInvariant(ModTestContext ctx)
        {
            ModAssert.True(RpgObjectNumbers.TryParse("0.5, 1.5", out var values), "two numbers read");
            ModAssert.Equal(0.5f, values[0], 1e-4f, "the point is the decimal sign");
            ModAssert.Equal(1.5f, values[1], 1e-4f, "the second number");
            ModAssert.True(RpgObjectNumbers.TryParse("", out var none) && none == null, "empty text is no value");
            ModAssert.False(RpgObjectNumbers.TryParse("1, two", out _), "a word is refused");
        }
    }
}
