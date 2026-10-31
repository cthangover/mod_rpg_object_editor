namespace Cthangover.Tools.RpgObjectEditor.Tests
{
    using System.Text.Json.Nodes;
    using Cthangover.Core.Mods.Testing;

    /// <summary>The variant id of a typed name and the number reading of the form (no Godot).</summary>
    public class RpgObjectVariantTests
    {
        [ModTest("variant/name_becomes_an_ascii_slug")]
        static void NameBecomesSlug(ModTestContext ctx)
        {
            ModAssert.Equal("winter_barrel", RpgObjectVariant.LocalId("  Winter  barrel!  ", "village_pack/container/barrel"), "spaces and marks");
            ModAssert.Equal("barrel2", RpgObjectVariant.LocalId("Barrel2", "x/y"), "digits are kept");
            ModAssert.Equal("mod/winter_barrel", RpgObjectVariant.IdFor("mod", "Winter barrel", "village_pack/container/barrel"), "the mod is the prefix");
        }

        [ModTest("variant/name_without_latin_takes_parent_segment")]
        static void NoLatinNameUsesParent(ModTestContext ctx)
        {
            ModAssert.Equal("barrel_variant", RpgObjectVariant.LocalId("Бочка", "village_pack/container/barrel"), "the parent's last segment");
            ModAssert.Equal("tree_variant", RpgObjectVariant.LocalId("", "tree"), "an empty name with a bare parent id");
        }

        [ModTest("numbers/whole_numbers_written_in_session_read_back")]
        static void WholeNumbersReadBack(ModTestContext ctx)
        {
            var written = RpgObjectNumbers.FromNode(RpgObjectNumbers.ToIntNode(new[] { 2, 1 }));
            ModAssert.Equal(2, written?.Length ?? 0, "two numbers");
            ModAssert.True(written[0] == 2f, "the width of an int node");
            var parsed = RpgObjectNumbers.FromNode(JsonNode.Parse("[3, 0.5]"));
            ModAssert.True(parsed[0] == 3f, "a parsed whole number");
            ModAssert.True(parsed[1] == 0.5f, "a parsed fraction");
        }
    }
}
