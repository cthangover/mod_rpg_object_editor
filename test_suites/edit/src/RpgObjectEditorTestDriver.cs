namespace Cthangover.Tools.RpgObjectEditor.Tests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text.Json.Nodes;
    using Cthangover.Core.Bootstrap;
    using Cthangover.Core.Localization;
    using Cthangover.Core.Mods;
    using Cthangover.Core.Mods.Resolvers;
    using Cthangover.Core.Utils;
    using Cthangover.Mods.Interface.Tool;
    using Cthangover.Mods.Rpg;
    using Cthangover.Mods.Rpg.Model;
    using Cthangover.Tools.EntityEditor;
    using Cthangover.Tools.Services;
    using Godot;

    /// <summary>
    /// Drives the RPG object editor like an author. On the own sprite: a state and a variant added and named, a
    /// part's geometry changed, the anchor dragged on the top-down preview, a scenario and then a teleport chosen
    /// in the pickers, a PNG imported from a temp folder, everything saved and read back by the game at once; the
    /// preview's projection toggle; the anchor of a foreign barrel saved as a patch while its source stays
    /// byte-identical. Afterwards every file the test may have touched is compared with its backup and written back.
    /// Result line: <c>[TEST_RESULT] name=rpg_object_editor_edit passed=N failed=M</c>.
    /// </summary>
    public partial class RpgObjectEditorTestDriver : Node
    {
        private const float TickSeconds = 0.15f;
        private const float TimeoutSeconds = 120f;
        private const string Mod = "rpg_plus_game";
        private const string SourceMod = "village_pack";
        private const string Barrel = "village_pack/container/barrel";
        private const string Own = "rpg_plus/container/supply_barrel";
        private const string PatchFile = "patches/rpg_object/rpg_plus_game.json";
        private const string ObjectsFile = "objects/rpg_plus_game.json";
        private const string VariantName = "Winter barrel";
        private const string VariantId = "rpg_plus_game/winter_barrel";
        private const string ParentOfOwn = "village_pack/container/barrel";
        private const string ForeignAnchor = "0.25, 1.25";
        private const string TestPictureName = "rpg_object_editor_test";

        private readonly List<string> failures = new();
        private readonly Dictionary<string, string> backups = new(StringComparer.Ordinal);
        private int passed;
        private int step;
        private bool restoredAll;
        private double elapsed;
        private RpgObjectEditorWindow window;
        private string sourceFile;
        private string sourceText;
        private string importedPicture;

        /// <summary>Creates the driver; a node of the scene tree ticking on a timer.</summary>
        public RpgObjectEditorTestDriver()
        {
            Name = "RpgObjectEditorTestDriver";
            var timer = new Timer { WaitTime = TickSeconds, Autostart = true };
            AddChild(timer);
            timer.Timeout += Tick;
        }

        private void Tick()
        {
            if (step < 0)
                return;
            elapsed += TickSeconds;
            if (elapsed > TimeoutSeconds)
            {
                Fail($"timeout at step {step}");
                Finish();
                return;
            }
            try
            {
                RunStep();
            }
            catch (Exception ex)
            {
                Fail($"step {step}: {ex.GetType().Name}: {ex.Message}");
                Finish();
            }
        }

        private void RunStep()
        {
            switch (step)
            {
                case 0:
                    if (!Boot.IsReady)
                        return;
                    foreach (var file in ModFileService.ListFiles(Mod, string.Empty, ".json").Concat(ModFileService.ListFiles(Mod, "locale", ".properties")))
                        backups[file] = ModFileService.ReadText(Mod, file);
                    backups[PatchFile] = ModFileService.ReadText(Mod, PatchFile);
                    // A variant made in this run lands in the mod's objects file: the file is removed afterwards when it did not exist.
                    if (!backups.ContainsKey(ObjectsFile))
                        backups[ObjectsFile] = null;
                    Check(backups[PatchFile] == null, "the mod has no object patch yet");
                    foreach (var file in ModFileService.ListFiles(SourceMod, string.Empty, ".json"))
                    {
                        var text = ModFileService.ReadText(SourceMod, file);
                        if (text != null && text.Contains("\"" + Barrel + "\""))
                        {
                            sourceFile = file;
                            sourceText = text;
                            break;
                        }
                    }
                    Check(sourceFile != null, "the source file of the foreign barrel is found");
                    window = ToolWindow.Open<RpgObjectEditorWindow>();
                    step++;
                    return;

                case 1:
                    if (window.Document == null)
                        return;
                    window.OpenMod(Mod);
                    var own = window.Document.Find(Own);
                    Check(own != null, "the own sprite is listed");
                    window.Select(own);
                    Check(window.Preview != null && window.Preview.VisualCount("topdown") > 0 && window.Preview.VisualCount("isometric") > 0,
                        "the preview draws the object in both projections");
                    // The sample barrel takes its parts from its Extends parent: the part is shown read-only, marked with the parent,
                    // and "Override" copies the parent's parts into the object, where they can be edited.
                    var inheritedRow = Named<Label>("Inherited_0");
                    Check(own.Data["Parts"] == null && inheritedRow != null && inheritedRow.Text.Contains(ParentOfOwn), "the inherited part is shown, marked with its parent");
                    Check(Named<Button>("InheritedOverride_0") is { } overrideButton && overrideButton.Text == TranslationServer.Translate("tools/rpg_object_editor/inherited_override"),
                        "the inherited part has an Override action");
                    Press("tools/rpg_object_editor/inherited_override");
                    Check(own.Data["Parts"] is JsonArray { Count: > 0 }, "Override copies the parent's parts into the object");
                    Check(Named<Label>("Inherited_0") == null, "an overridden part is no longer shown as inherited");
                    Check(Named<LineEdit>("Part0_Id") != null, "the copied part is editable in the parts table");
                    step++;
                    return;

                case 2:
                    // A state and a variant, named in the blocks (Enter applies a name).
                    Press("tools/rpg_object_editor/state_add");
                    var states = OwnRecord().Data["States"] as JsonArray;
                    Check(states is { Count: 1 }, "add state makes one state");
                    Submit(Named<LineEdit>("StateId"), "open");
                    Submit(Named<LineEdit>("StateWhen"), "self.open == 1");
                    Check(OwnRecord().Data["States"]?[0]?["Id"]?.ToString() == "open", "the state takes the typed id");
                    Check(OwnRecord().Data["States"]?[0]?["When"]?.ToString() == "self.open == 1", "the state takes its condition");
                    Submit(Named<LineEdit>("VariantName"), "winter");
                    Press("tools/rpg_object_editor/variant_add");
                    Check(OwnRecord().Data["Variants"] is JsonObject { } variants && variants.ContainsKey("winter"), "add variant makes the named variant");
                    step++;
                    return;

                case 3:
                    // The preview shows the variant (the first pick after the default) and the part's geometry is edited.
                    ChooseItem(Named<OptionButton>("PreviewChoice"), "variant:winter");
                    Check(window.Preview.VisualCount("topdown") > 0, "the variant preview draws the object");
                    Pick(Named<OptionButton>("Part0_Geometry"), 1);
                    Check(OwnRecord().Data["Parts"]?[0]?["Geometry"]?.ToString() == "ground", "the part's geometry is written");
                    step++;
                    return;

                case 4:
                    // The anchor dragged on the top-down preview: cell (SpriteX + 1, SpriteY + 2) is the cell centre (1.5, 2.5).
                    Check(window.Preview.DragAnchorToCell("topdown", RpgObjectSample.SpriteX + 1, RpgObjectSample.SpriteY + 2),
                        "the anchor can be dragged on the top-down preview");
                    var dragged = OwnRecord().Data["Anchor"] as JsonArray;
                    Check(dragged is { Count: 2 } && (double)dragged[0] == 1.5 && (double)dragged[1] == 2.5, "the drag writes the anchor cell centre");
                    Check(window.Preview.Anchor is { Length: 2 } a && a[0] == 1.5f && a[1] == 2.5f, "the handle sits where the drag ended");
                    Press("tools/common/save");
                    var game = Entities.Collect<RpgObject>();
                    Check(game.TryGetValue(Own, out var saved) && saved.Anchor is { Length: 2 } && saved.Anchor[0] == 1.5f && saved.Anchor[1] == 2.5f,
                        "the game knows the dragged anchor at once");
                    Check(saved != null && saved.States is { Count: 1 } && saved.States[0].Id == "open", "the game knows the state");
                    Check(saved != null && saved.Variants != null && saved.Variants.ContainsKey("winter"), "the game knows the variant");
                    Check(saved != null && saved.Parts is { Count: > 0 } && saved.Parts[0].Geometry == RpgPartGeometry.Ground, "the game knows the part's geometry");
                    step++;
                    return;

                case 5:
                    // Interact through the scenario picker (the interaction is added first when the object has none).
                    if (Named<Button>("Object_InteractAdd") is { } add)
                        add.EmitSignal(BaseButton.SignalName.Pressed);
                    Press("tools/rpg_object_editor/scenario_pick");
                    var scenarioPicker = Dialog();
                    Check(scenarioPicker != null, "the scenario picker opens");
                    scenarioPicker?.Choose("rpg_plus/events/house_chest");
                    Check(OwnRecord().Data["Interact"]?["Scenario"]?.ToString() == "rpg_plus/events/house_chest", "the scenario picker writes the scenario");
                    Press("tools/common/save");
                    var withScenario = Entities.Collect<RpgObject>();
                    Check(withScenario.TryGetValue(Own, out var interacting) && interacting.Interact?.Scenario == "rpg_plus/events/house_chest",
                        "the game knows the scenario at once");
                    step++;
                    return;

                case 6:
                    // Interact through the map picker: a teleport takes the place of the scenario.
                    var map = Entities.Collect<RpgMap>().Keys.OrderBy(k => k, StringComparer.Ordinal).First();
                    Press("tools/rpg_object_editor/map_pick");
                    Dialog()?.Choose(map);
                    Check(OwnRecord().Data["Interact"]?["Teleport"]?["Map"]?.ToString() == map, "the map picker writes the teleport");
                    Check(OwnRecord().Data["Interact"]?["Scenario"] == null, "a teleport drops the scenario");
                    Press("tools/common/save");
                    var jumping = Entities.Collect<RpgObject>();
                    Check(jumping.TryGetValue(Own, out var teleporting) && teleporting.Interact?.Teleport?.Map == map, "the game knows the teleport at once");
                    step++;
                    return;

                case 7:
                    // A PNG copied from a temp folder into rpg_images of the mod, and a file that is not a picture refused.
                    var category = OwnRecord().Data["Category"]?.ToString() ?? "misc";
                    var picturePath = Path.Combine(Path.GetTempPath(), "rpg_object_editor_import_test.png");
                    var textPath = Path.Combine(Path.GetTempPath(), "rpg_object_editor_import_test.txt");
                    var image = Image.CreateEmpty(64, 64, false, Image.Format.Rgba8);
                    image.Fill(Colors.Red);
                    Check(image.SavePng(picturePath) == Error.Ok, "a test PNG is written in the temp folder");
                    File.WriteAllText(textPath, "not a picture");
                    backups["rpg_images/" + category + "/" + TestPictureName + ".png"] = null;
                    var refused = window.ImportPicture(textPath, "not_a_picture");
                    Check(refused == null && !File.Exists(ModFileService.GetWritablePath(Mod, "rpg_images/" + category + "/not_a_picture.png")),
                        "a file that is not a PNG is refused and nothing is copied");
                    importedPicture = window.ImportPicture(picturePath, TestPictureName);
                    Check(importedPicture == category + "/" + TestPictureName, "the PNG is imported under the object's category");
                    var copied = importedPicture == null ? null : ModFileService.GetWritablePath(Mod, "rpg_images/" + importedPicture + ".png");
                    Check(copied != null && File.Exists(copied), "the PNG is copied into rpg_images of the mod");
                    Check(OwnRecord().Data["Parts"]?[0]?["Image"]?.ToString() == importedPicture, "the object's first part shows the imported picture");
                    Press("tools/common/save");
                    var pictured = Entities.Collect<RpgObject>();
                    Check(pictured.TryGetValue(Own, out var picture) && picture.Parts is { Count: > 0 } && picture.Parts[0].Image == importedPicture,
                        "the game knows the imported picture at once");
                    File.Delete(picturePath);
                    File.Delete(textPath);
                    step++;
                    return;

                case 8:
                    var barrel = window.Document.FindAny(Barrel);
                    Check(barrel != null && barrel.IsForeign, "the foreign barrel is listed as another mod's entity");
                    window.Select(barrel);
                    Type(FieldEdit("tools/rpg_object_editor/anchor"), ForeignAnchor);
                    Check(window.Document.PatchOf(Barrel) != null, "the edit of a foreign entity is a patch in the own mod");
                    Press("tools/common/save");
                    var patch = ModFileService.ReadText(Mod, PatchFile) ?? string.Empty;
                    Check(patch.Contains("\"" + Barrel + "\"") && patch.Contains("1.25"), "the patch file holds the barrel's anchor");
                    Check(ModFileService.ReadText(SourceMod, sourceFile) == sourceText, "the source file of the barrel stays byte-identical");
                    var after = Entities.Collect<RpgObject>();
                    Check(after.TryGetValue(Barrel, out var barrelNow) && barrelNow.Anchor is { Length: 2 } && barrelNow.Anchor[1] == 1.25f,
                        "the game sees the patched anchor of the barrel");
                    step++;
                    return;

                case 9:
                    window.Preview.SelectProjection("isometric");
                    Check(Shown("isometric"), "isometric only: the isometric preview is shown");
                    Check(!Shown("topdown"), "isometric only: the top-down preview is hidden");
                    window.Preview.SelectProjection("topdown");
                    Check(Shown("topdown") && !Shown("isometric"), "top-down only: switched");
                    window.Preview.SelectProjection("both");
                    Check(Shown("topdown") && Shown("isometric"), "both: the two previews side by side");
                    step++;
                    return;

                case 10:
                    // The ground around the object: a dropdown of the surfaces, grass by default; the switch redraws the preview.
                    window.ShowObject(Own);
                    var neighbourPicker = Named<OptionButton>("PreviewNeighbour");
                    Check(neighbourPicker != null && window.Preview.Surfaces.Count > 1, "the ground dropdown lists the surfaces");
                    Check(window.Preview.Neighbour == "village_pack/ground/grass", "the ground around is grass by default");
                    Pick(neighbourPicker, 1);
                    Check(window.Preview.Neighbour == window.Preview.Surfaces[1], "switching the dropdown changes the ground around");
                    Check(window.Preview.VisualCount("topdown") > 0 && window.Preview.VisualCount("isometric") > 0, "the preview is redrawn on the new ground");
                    Check(window.Preview.FigureCount == 2, "two player figures stand next to the sprite (behind and in front)");
                    step++;
                    return;

                case 11:
                    // The footprint brush: a press on a cell inside the footprint flips its mask, a press outside grows the footprint.
                    Pick(Named<OptionButton>("PreviewBrush"), 1);
                    Check(window.Preview.Brush == RpgObjectPreview.FootprintBrush, "the brush is the footprint");
                    var maskBefore = Named<Button>("Mask_0_0")?.Text;
                    Check(window.Preview.ClickFootprintCell("topdown", RpgObjectSample.SpriteX, RpgObjectSample.SpriteY), "a press on the footprint cell is taken");
                    var maskAfter = Named<Button>("Mask_0_0")?.Text;
                    Check(maskBefore != null && maskAfter != null && maskBefore != maskAfter, "the press flips the cell's mask");
                    step++;
                    return;

                case 12:
                    // A second press, a tick later: the preview is laid out again after the first press rebuilt it.
                    var widthBefore = OwnRecord().Data["Footprint"]?["Size"] is JsonArray size ? (int)size[0] : 1;
                    Check(window.Preview.ClickFootprintCell("topdown", RpgObjectSample.SpriteX + 1, RpgObjectSample.SpriteY), "a press right of the footprint is taken");
                    Check(Named<Button>("Mask_1_0") != null, "the footprint grows to the cell pressed");
                    Check(OwnRecord().Data["Footprint"]?["Size"] is JsonArray { Count: 2 } grown && (int)grown[0] == widthBefore + 1, "the footprint width is written");
                    Press("tools/common/save");
                    var footprinted = Entities.Collect<RpgObject>();
                    Check(footprinted.TryGetValue(Own, out var footed) && footed.Footprint?.Width == 2, "the game knows the grown footprint at once");
                    step++;
                    return;

                case 13:
                    // Make a variant of the foreign barrel: a new own object that inherits the barrel and is known to the game.
                    window.ShowObject(Barrel);
                    Check(window.SelectedId == Barrel && window.Document.FindAny(Barrel).IsForeign, "the barrel is selected as a foreign object");
                    Press("tools/rpg_object_editor/make_variant");
                    var nameEdit = Named<LineEdit>("MakeVariantName");
                    Check(nameEdit != null, "Make a variant asks for a name");
                    if (nameEdit != null)
                        Submit(nameEdit, VariantName);
                    var made = window.Document.Find(VariantId);
                    Check(made != null && !made.IsForeign, "the variant is a new own object of the mod");
                    Check(window.SelectedId == VariantId, "the variant opens in the form");
                    Check(made?.Data?["Extends"]?.ToString() == Barrel, "the variant extends the barrel");
                    Press("tools/common/save");
                    var all = Entities.Collect<RpgObject>();
                    var barrelParts = all.TryGetValue(Barrel, out var barrelNow2) ? barrelNow2.Parts : null;
                    Check(all.TryGetValue(VariantId, out var variant) && variant.Extends == Barrel, "the game knows the variant and its parent");
                    Check(variant != null && barrelParts is { Count: > 0 } && variant.Parts is { Count: > 0 } && variant.Parts[0].Image == barrelParts[0].Image,
                        "the variant inherits the barrel's picture");
                    step++;
                    return;

                case 14:
                    Finish();
                    return;
            }
        }

        private EntityRecord OwnRecord() => window.Document.Find(Own);

        private bool Shown(string projection) => window.Preview.ShownProjections.Contains(projection);

        private T Named<T>(string name) where T : Node => window.FindChild(name, true, false) as T;

        private RpgChoicePickerDialog Dialog()
            => window.FindChildren("*", "", true, false).OfType<RpgChoicePickerDialog>().FirstOrDefault();

        private static void Submit(LineEdit edit, string text)
        {
            edit.Text = text;
            edit.EmitSignal(LineEdit.SignalName.TextSubmitted, text);
        }

        private static void Pick(OptionButton picker, int index)
        {
            picker.Select(index);
            picker.EmitSignal(OptionButton.SignalName.ItemSelected, index);
        }

        /// <summary>Picks the item whose text names <paramref name="value"/> (the blocks show a variant as "variant: name").</summary>
        private static void ChooseItem(OptionButton picker, string value)
        {
            var name = value.Substring(value.IndexOf(':') + 1);
            var index = Enumerable.Range(0, picker.ItemCount).First(i => picker.GetItemText(i).EndsWith(name, StringComparison.Ordinal));
            Pick(picker, index);
        }

        private static void Type(LineEdit edit, string text)
        {
            edit.Text = text;
            edit.EmitSignal(LineEdit.SignalName.TextChanged, text);
        }

        private LineEdit FieldEdit(string labelKey)
        {
            var label = TranslationServer.Translate(labelKey).ToString();
            var found = window.FindChildren("*", "", true, false).OfType<Label>()
                .First(l => l.Text == label && l.IsVisibleInTree());
            return found.GetParent().GetChildren().OfType<LineEdit>().First();
        }

        private void Press(string key)
        {
            var text = TranslationServer.Translate(key);
            var button = window.FindChildren("*", "", true, false).OfType<Button>().FirstOrDefault(b => b.Text == text);
            if (button == null)
            {
                Fail($"button '{text}' not found");
                return;
            }
            button.EmitSignal(BaseButton.SignalName.Pressed);
        }

        private void Check(bool condition, string what)
        {
            if (condition)
            {
                passed++;
                GameLogger.Log("TEST", $"[PASS] {what}");
            }
            else
            {
                Fail(what);
            }
        }

        private void Fail(string what)
        {
            failures.Add(what);
            GameLogger.Log("TEST", $"[FAIL] {what}", LogLevel.Error);
        }

        /// <summary>Puts the files the test may have touched back as they were; each restored file is a check of its own.</summary>
        private void RestoreAll()
        {
            var restored = 0;
            var differs = new List<string>();
            foreach (var backup in backups)
            {
                var current = ModFileService.ReadText(Mod, backup.Key);
                if (current == backup.Value)
                    continue;
                differs.Add(backup.Key);
                if (backup.Value == null)
                    ModFileService.DeleteFile(Mod, backup.Key);
                else
                    ModFileService.WriteText(Mod, backup.Key, backup.Value);
                restored++;
            }
            ModManager.Instance.GetResolver<PatchResolver>()?.ClearCache();
            ModManager.Instance.GetResolver<EntityResolver>()?.ClearCache();
            ModManager.Instance.GetResolver<FileListResolver>()?.ClearCache();
            LocaleFileService.ReloadGame();
            GameLogger.Log("TEST", $"rpg_object_editor_edit: {restored} file(s) written back: {string.Join(", ", differs)}");
            var still = backups.Where(b => ModFileService.ReadText(Mod, b.Key) != b.Value).Select(b => b.Key).ToList();
            Check(still.Count == 0, "the mod's files are as they were before the test");
            Check(ModFileService.ReadText(SourceMod, sourceFile) == sourceText, "the source file is as it was");
        }

        /// <summary>Ends the run: restores the files (also after a failed step), prints the result and quits.</summary>
        private void Finish()
        {
            step = -1;
            if (!restoredAll)
            {
                restoredAll = true;
                try
                {
                    RestoreAll();
                }
                catch (Exception ex)
                {
                    Fail($"restore: {ex.GetType().Name}: {ex.Message}");
                }
            }
            window?.QueueFree();
            GameLogger.Log("TEST", $"[TEST_RESULT] name=rpg_object_editor_edit passed={passed} failed={failures.Count}");
            ApplicationExit.Quit(GetTree(), failures.Count == 0 ? 0 : 1);
        }
    }
}
