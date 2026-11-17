namespace Cthangover.Tools.RpgObjectEditor.Tests.Visual
{
    using System;
    using System.IO;
    using System.Text.Json;
    using Cthangover.Core.Bootstrap;
    using Cthangover.Core.Utils;
    using Cthangover.Mods.Interface.Tool;
    using Cthangover.Mods.Rpg.Model;
    using Cthangover.Mods.Rpg;
    using Cthangover.Mods.TileRender;
    using Cthangover.Mods.Rpg.Play;
    using Cthangover.Tools.Services;
    using Godot;

    /// <summary>
    /// Real room artwork exercises projected drags, undo, sort point, camera and Apply on detached editing data.
    /// </summary>
    public partial class RpgVisualPartsDriver : Node
    {
        private const string Mod = "junior_reaper_paper_grief";
        private const string Id = Mod + "/interior/bureau_hall";
        private RpgObjectEditorWindow owner;
        private RpgPartEditorWindow window;
        private RpgObjectPreview canvas;
        private string original;
        private string file;
        private string shots;
        private int step;
        private int initialParts;
        private int overlay;
        private int passed;
        private int failed;
        private double elapsed;
        private Vector2 start;
        private Vector2 end;
        private string fixtureOriginal;
        private RpgDepthPixels pixels;
        private RpgWorldSession liveBefore;
        private Vector2I playerBefore;
        private float oldX;
        private float oldWidth;
        private float oldHeight;
        private readonly Timer timer = new()
        {
            WaitTime = 0.4,
            Autostart = true
        };
        /// <summary>Connects the owned timer only after the native tree can receive the real GUI input sequence.</summary>
        public override void _Ready()
        {
            foreach (var arg in OS.GetCmdlineUserArgs())
            {
                if (arg.StartsWith("--shots=", StringComparison.Ordinal))
                {
                    shots = arg.Substring(8);
                }
            }

            AddChild(timer);
            timer.Timeout += Tick;
        }

        private void Tick()
        {
            elapsed += timer.WaitTime;
            if (elapsed > 60)
            {
                Check(false, "timeout " + step);
                Finish();
                return;
            }

            try
            {
                Run();
            }
            catch (Exception ex)
            {
                Check(false, ex.ToString());
                Finish();
            }
        }

        private void Run()
        {
            switch (step)
            {
                case 0:
                    if (!Boot.IsReady)
                    {
                        return;
                    }

                    file = ModFileService.ReadText(Mod, "objects/interior/bureau_rooms.json");
                    owner = ToolWindow.Open<RpgObjectEditorWindow>();
                    owner.OpenMod(Mod);
                    owner.Select(owner.Document.Find(Id));
                    original = owner.Document.Find(Id).Data.ToJsonString();
                    owner.OpenVisualParts();
                    window = owner.PartsWindow;
                    canvas = window.Canvas;
                    initialParts = canvas.EditableParts.Count;
                    overlay = initialParts;
                    break;
                case 1:
                    Check(canvas.PartCanvas("isometric").Size.Y >= 500, "large visual canvas visible");
                    var layers = (ItemList)window.FindChild("Parts", true, false);
                    Check(layers.Size.Y > 400 && layers.GlobalPosition.X < canvas.PartCanvas("isometric").GlobalPosition.X,
                         "persistent parts list is visible on the left of the canvas");
                    Check(canvas.EditableParts.Count == initialParts && initialParts > 0, "original room parts loaded");
                    for (var i = 0; i < initialParts; i++)
                    {
                        if (canvas.EditableParts[i].Image == "interior/stas_office_personal_desk")
                        {
                            canvas.SelectPart(i);
                            canvas.SetPartOrder(TileDrawOrder.BelowCharacters);
                        }
                    }

                    canvas.SelectPart(0);
                    canvas.AddOverlayPart("interior/stas_office_personal_desk");
                    Check(canvas.EditableParts.Count == initialParts + 1 && canvas.SelectedPart == overlay, "overlay selected");
                    layers.Select(overlay);
                    layers.EmitSignal(ItemList.SignalName.ItemSelected, (long)overlay);
                    Check(canvas.SelectedPart == overlay, "layer selection follows the real list control");
                    Check(canvas.EditableParts[overlay].SortOffset != null, "new overlay gets a depth point from its visible image");
                    Check(canvas.EditableParts[overlay].At[0] == canvas.EditableParts[0].At[0] 
                        && canvas.EditableParts[overlay].Size[0] == canvas.EditableParts[0].Size[0],
                         "overlay aligns automatically without entering numbers");
                    Check(owner.Document.Find(Id).Data.ToJsonString() == original, "working copy does not mutate parent");
                    break;
                case 2:
                    var corners = canvas.PartCorners("isometric", overlay);
                    start = corners[0] + (corners[1] - corners[0]) * (960f / 1920) + (corners[3] - corners[0]) * (640f / 1280);
                    oldX = canvas.EditableParts[overlay].At[0];
                    end = start + new Vector2(45, 22);
                    Press(start);
                    Move(end);
                    Check(canvas.EditableParts[overlay].At[0] > oldX, "opaque desk is picked and moved during drag");
                    Release(end);
                    canvas.UndoParts();
                    Check(Math.Abs(canvas.EditableParts[overlay].At[0] - oldX) < 0.001, "one undo restores picture position");
                    canvas.RedoParts();
                    Check(canvas.EditableParts[overlay].At[0] > oldX, "redo restores move");
                    canvas.UndoParts();
                    oldWidth = canvas.EditableParts[overlay].Size[0];
                    oldHeight = canvas.EditableParts[overlay].Size[1];
                    corners = canvas.PartCorners("isometric", overlay);
                    start = corners[2];
                    end = start + new Vector2(35, 25);
                    Press(start);
                    Move(end);
                    Release(end);
                    Check(canvas.EditableParts[overlay].Size[0] > oldWidth && canvas.EditableParts[overlay].Size[1] > oldHeight,
                         "corner drag changes picture size in isometric view");
                    canvas.UndoParts();
                    break;
                case 3:
                    canvas.FocusSelectedPart();
                    Mode(1);
                    start = canvas.PartWorldPoint("isometric", new Vector2(18, 1));
                    Press(start);
                    Release(start);
                    Check(Math.Abs(canvas.EditableParts[overlay].SortOffset[0] - 18) < 0.001 
                        && Math.Abs(canvas.EditableParts[overlay].SortOffset[1] - 1) < 0.001,
                         "click places correct sort point in room cell coordinates");
                    Check(canvas.EditableParts[overlay].DrawOrder == Cthangover.Mods.TileRender.TileDrawOrder.ByPosition,
                         "depth brush enables positional sorting");
                    var beforeProbe = JsonSerializer.Serialize(canvas.EditableParts, JsonOptionsProvider.ReadOptions);
                    Mode(2);
                    start = canvas.PartWorldPoint("isometric", new Vector2(17, 1));
                    Press(start);
                    Release(start);
                    Check(canvas.ProbePosition is { } probe && probe.DistanceTo(new Vector2(19, 3)) < 0.001f,
                         "test character stands on the clicked world point");
                    Check(JsonSerializer.Serialize(canvas.EditableParts, JsonOptionsProvider.ReadOptions) == beforeProbe,
                         "moving test character does not edit parts");
                    break;
                case 4:
                    Shot("visual_desk_behind");
                    canvas.PlaceProbeSide(false);
                    Check(!canvas.ProbeIsInFront("isometric"), "behind preset puts the desk in front");
                    canvas.PlaceProbeSide(true);
                    Check(canvas.ProbeIsInFront("isometric"), "front preset puts the character in front");
                    break;
                case 5:
                    Shot("visual_desk_front");
                    var feet = canvas.ProbePosition.Value;
                    start = canvas.PartWorldPoint("isometric", feet - new Vector2(2, 2));
                    Mode(0);
                    Press(start);
                    Move(start + new Vector2(20, 10));
                    Release(start + new Vector2(20, 10));
                    Check(canvas.ProbePosition.Value.DistanceTo(feet) > 0.1f,
                         "blue foot handle drags the character even in selection mode");
                    var json = JsonSerializer.Serialize(canvas.EditableParts, JsonOptionsProvider.ReadOptions);
                    var restored = JsonSerializer.Deserialize<System.Collections.Generic.List<RpgObjectPart>>(json,
                         JsonOptionsProvider.ReadOptions);
                    Check(restored.Count == initialParts + 1 && Math.Abs(restored[overlay].SortOffset[0] - 18) < 0.001,
                         "visual changes survive JSON roundtrip");
                    Mode(0);
                    canvas.SelectProjection("topdown");
                    // The room parts are explicitly isometric; top-down correctly hides them.
                    Check(canvas.PartCorners("topdown", overlay) == null, "projection restriction is respected");
                    canvas.SelectProjection("isometric");
                    var c = canvas.PartCorners("isometric", overlay);
                    start = c[2];
                    oldWidth = canvas.EditableParts[overlay].Size[0];
                    Press(start);
                    Move(start + new Vector2(25, 10));
                    canvas.CancelPartDrag();
                    Release(start);
                    Check(Math.Abs(canvas.EditableParts[overlay].Size[0] - oldWidth) < 0.001,
                         "Escape contract cancels pending drag without history");
                    var beforeCamera = JsonSerializer.Serialize(canvas.EditableParts, JsonOptionsProvider.ReadOptions);
                    var oldCorners = canvas.PartCorners("isometric", overlay);
                    Input(new InputEventMouseButton { 
                        ButtonIndex = MouseButton.WheelUp, 
                        Pressed = true, 
                        Position = oldCorners[0] 
                    });
                    var zoomed = canvas.PartCorners("isometric", overlay);
                    Check(zoomed[0].DistanceTo(zoomed[1]) > oldCorners[0].DistanceTo(oldCorners[1]), "wheel enlarges view around pointer");
                    Input(new InputEventMouseButton { 
                        ButtonIndex = MouseButton.Middle, 
                        Pressed = true, 
                        Position = zoomed[0] 
                    });
                    Input(new InputEventMouseMotion { 
                        Position = zoomed[0] + new Vector2(20, 10), 
                        Relative = new Vector2(20, 10), 
                        ButtonMask = MouseButtonMask.Middle 
                    });
                    Input(new InputEventMouseButton { 
                        ButtonIndex = MouseButton.Middle, 
                        Position = zoomed[0] + new Vector2(20, 10) 
                    });
                    Check(canvas.PartCorners("isometric", overlay)[0].DistanceTo(zoomed[0] + new Vector2(20, 10)) < 0.01f,
                         "middle button pans view without jumping");
                    Check(JsonSerializer.Serialize(canvas.EditableParts, JsonOptionsProvider.ReadOptions) == beforeCamera,
                         "camera changes never alter part geometry");
                    window.Apply();
                    break;
                case 6:
                    Check(owner.Document.Find(Id).Data["Parts"].AsArray().Count == initialParts + 1, "Apply changes parent document");
                    Check(ModFileService.ReadText(Mod, "objects/interior/bureau_rooms.json") == file,
                         "Apply never silently saves production artwork setup");
                    owner.Document.Undo();
                    Check(owner.Document.Find(Id).Data.ToJsonString() == original, "parent undo restores entire pre-workspace object");
                    owner.QueueFree();
                    owner = ToolWindow.Open<RpgObjectEditorWindow>();
                    owner.OpenMod("rpg_object_editor__visual");
                    fixtureOriginal = ModFileService.ReadText("rpg_object_editor__visual", "objects/live_fixture.json");
                    owner.Select(owner.Document.Find("rpg_object_editor__visual/live_fixture"));
                    owner.OpenVisualParts();
                    window = owner.PartsWindow;
                    canvas = window.Canvas;
                    canvas.SetPartOrder(TileDrawOrder.AboveCharacters);
                    window.Apply();
                    liveBefore = RpgWorldHost.Current.Session;
                    playerBefore = liveBefore.Player.Cell;
                    Check(owner.Save(), "fixture object saved through the normal editor");
                    break;
                case 7:
                    var host = RpgWorldHost.Current;
                    Check(host.Session.Player.Cell == playerBefore, "saving object preserves player cell");
                    TileDrawOrder? actual = null;
                    foreach (var placed in host.Session.Statics)
                    {
                        if (placed.Id == "jrp_bureau_hall/visual_test_fixture")
                        {
                            actual = placed.Object.Parts[0].DrawOrder;
                        }
                    }

                    Check(actual == TileDrawOrder.AboveCharacters,
                         "mounted world reads the newly saved part order without re-entering scene");
                    pixels = new RpgDepthPixels();
                    AddChild(pixels);
                    break;
                case 8:
                    var behind = pixels.Pixel();
                    Check(behind.G > behind.R + 0.2f, "real renderer pixel: desk covers a character behind it");
                    pixels.SetFront(true);
                    break;
                case 9:
                    var front = pixels.Pixel();
                    Check(front.R > front.G + 0.2f, "real renderer pixel: character covers the desk when in front");
                    Finish();
                    return;
            }

            step++;
        }

        private void Mode(int index)
        {
            var name = index == 0 ? "MovePart" : index == 1 ? "DepthPart" : "MovePlayer";
            var button = (Button)window.FindChild(name, true, false);
            button.ButtonPressed = true;
            button.EmitSignal(BaseButton.SignalName.Pressed);
        }

        private void Input(InputEvent ev)
        {
            canvas.PartCanvas("isometric").EmitSignal(Control.SignalName.GuiInput, ev);
        }

        private void Press(Vector2 p)
        {
            Input(new InputEventMouseButton { 
                ButtonIndex = MouseButton.Left, 
                Pressed = true, 
                Position = p 
            });
        }

        private void Release(Vector2 p)
        {
            Input(new InputEventMouseButton { 
                ButtonIndex = MouseButton.Left, 
                Position = p 
            });
        }

        private void Move(Vector2 p)
        {
            Input(new InputEventMouseMotion { 
                Position = p, 
                ButtonMask = MouseButtonMask.Left 
            });
        }

        private void Shot(string name)
        {
            if (shots == null)
            {
                return;
            }

            Directory.CreateDirectory(shots);
            using var image = GetViewport().GetTexture().GetImage();
            image.SavePng(Path.Combine(shots, name + ".png"));
        }

        private void Check(bool ok, string text)
        {
            if (ok)
            {
                passed++;
            }
            else
            {
                failed++;
            }

            GameLogger.Log("TEST", (ok ? "[PASS] " : "[FAIL] ") + text);
        }

        private void Finish()
        {
            timer.Stop();
            if (fixtureOriginal != null)
            {
                ModFileService.WriteText("rpg_object_editor__visual", "objects/live_fixture.json", fixtureOriginal);
                fixtureOriginal = null;
            }

            if (GodotObject.IsInstanceValid(pixels))
            {
                pixels.QueueFree();
            }

            if (GodotObject.IsInstanceValid(window))
            {
                window.QueueFree();
            }

            owner?.QueueFree();
            GameLogger.Log("TEST", "[TEST_RESULT] name=rpg_visual_parts passed=" + passed + " failed=" + failed);
            ApplicationExit.Quit(GetTree(), failed == 0 ? 0 : 1);
        }
    }
}
