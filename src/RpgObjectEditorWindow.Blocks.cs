namespace Cthangover.Tools.RpgObjectEditor
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.Json.Nodes;
    using Cthangover.Core.Mods.Resolvers;
    using Cthangover.Core.UI.Theming;
    using Cthangover.Mods.Interface.Theming;
    using Cthangover.Mods.Interface.Tool;
    using Cthangover.Mods.Rpg;
    using Cthangover.Mods.Rpg.Model;
    using Cthangover.Tools.Common;
    using Cthangover.Tools.EntityEditor;
    using Godot;

    /// <summary>
    /// The blocks under the preview: each one reads the object (or a state, a variant, a light) from the
    /// selected entity and writes a change through <see cref="Edit"/>. The blocks are rebuilt after every
    /// change, so they always show what the JSON says; while they are built, the controls do not write.
    /// </summary>
    public partial class RpgObjectEditorWindow
    {
        private static readonly string[] Geometries = { "upright", "ground", "plane", "quad" };
        private static readonly string[] Alongs = { "auto", "x", "y" };
        private static readonly string[] Directions = { "Down", "Left", "Right", "Up" };
        private static readonly string[] DrawOrders = { "BelowCharacters", "ByPosition", "AboveCharacters" };

        private static readonly Func<JsonObject, JsonObject> WholeObject = root => root;

        // ── rebuilding ─────────────────────────────────────────────

        private void Refresh() => RebuildBlocks(Document?.FindAny(SelectedId));

        private void RebuildBlocks(EntityRecord record)
        {
            building = true;
            try
            {
                foreach (var child in blocksHost.GetChildren())
                {
                    blocksHost.RemoveChild(child);
                    child.QueueFree();
                }
                var data = record?.Data;
                if (data == null)
                {
                    preview?.Clear();
                    return;
                }

                var form = FormOf(data);
                var sprite = form == "sprite";
                var behaviour = sprite || form == "actor";
                AddMakeVariant();
                AddPreviewChoice(record);
                AddImport(form);
                if (sprite)
                {
                    AddParts(data, WholeObject, "Part", inherit: true);
                    AddFootprint(data);
                }
                AddFlags(data, form, WholeObject, true);
                if (behaviour)
                {
                    AddInteract(data, WholeObject, "Object");
                    AddStates(data, form);
                    AddLights(data);
                    AddRoute(data);
                }
                AddVariants(data, form);
                RenderPreview(record);
            }
            finally
            {
                building = false;
            }
        }

        // ── small control builders ─────────────────────────────────

        private void AddRow(params Control[] items)
        {
            var row = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            row.AddThemeConstantOverride("separation", UiTokens.Space2);
            foreach (var item in items)
                row.AddChild(item);
            blocksHost.AddChild(row);
        }

        private void Section(string titleKey) => blocksHost.AddChild(ToolUi.Section(titleKey));

        private Button ButtonOf(string textKey, Action pressed, string name = null)
        {
            var button = ToolUi.Button(textKey, pressed, ButtonKind.Secondary);
            if (name != null)
                button.Name = name;
            return button;
        }

        private OptionButton PickField(string labelKey, string tipKey, IReadOnlyList<(string Value, string Text)> items, string current, Action<string> onPick, string name = null)
        {
            var picker = new OptionButton { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            if (name != null)
                picker.Name = name;
            var index = 0;
            for (var i = 0; i < items.Count; i++)
            {
                picker.AddItem(items[i].Text, i);
                if (items[i].Value == (current ?? string.Empty))
                    index = i;
            }
            picker.Select(index);
            picker.ItemSelected += i =>
            {
                if (!building)
                    onPick(items[(int)i].Value);
            };
            ToolUi.Field(blocksHost, labelKey, picker, tipKey);
            return picker;
        }

        private LineEdit TextField(string labelKey, string tipKey, string value, Action<string> onText, string name = null)
        {
            var original = value ?? string.Empty;
            var edit = new LineEdit { Text = original, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            if (name != null)
                edit.Name = name;
            void Apply()
            {
                if (!building && edit.Text.Trim() != original)
                    onText(edit.Text.Trim());
            }
            edit.TextSubmitted += _ => Apply();
            edit.FocusExited += Apply;
            ToolUi.Field(blocksHost, labelKey, edit, tipKey);
            return edit;
        }

        private void PictureField(string labelKey, string tipKey, string value, string group, Action<string> onText, string name)
        {
            var original = value ?? string.Empty;
            var edit = new LineEdit { Text = original, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, Name = name };
            void Apply()
            {
                if (!building && edit.Text.Trim() != original)
                    onText(edit.Text.Trim());
            }
            edit.TextSubmitted += _ => Apply();
            edit.FocusExited += Apply;
            var pick = UiKit.IconButton("…", ToolUi.Tr("tools/entity_editor/picture_pick_tip"));
            pick.Pressed += () =>
            {
                var dialog = new PicturePickerDialog(group, edit.Text.Trim());
                dialog.Picked += id => onText(id);
                AddChild(dialog);
                dialog.PopupCentered();
            };
            var row = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            row.AddChild(edit);
            row.AddChild(pick);
            ToolUi.Field(blocksHost, labelKey, row, tipKey);
        }

        private SpinBox SpinField(string labelKey, string tipKey, double value, double min, double max, double step, Action<double> onValue, string name = null)
        {
            var spin = ToolUi.Number(min, max, step, v =>
            {
                if (!building)
                    onValue(v);
            });
            spin.Value = value;
            if (name != null)
                spin.Name = name;
            ToolUi.Field(blocksHost, labelKey, spin, tipKey);
            return spin;
        }

        private void CheckField(string labelKey, string tipKey, bool value, Action<bool> onToggle, string name = null)
        {
            var check = new CheckBox { Text = ToolUi.Tr(labelKey), ButtonPressed = value, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            if (tipKey != null)
                check.TooltipText = ToolUi.Tr(tipKey);
            if (name != null)
                check.Name = name;
            check.Toggled += on =>
            {
                if (!building)
                    onToggle(on);
            };
            blocksHost.AddChild(check);
        }

        private ItemList ListOf(string name, IEnumerable<string> items, int selected)
        {
            var list = new ItemList { Name = name, CustomMinimumSize = new Vector2(0, 96), SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            foreach (var item in items)
                list.AddItem(item);
            if (selected >= 0 && list.ItemCount > selected)
                list.Select(selected);
            blocksHost.AddChild(list);
            return list;
        }

        private void Invalid() => SetStatus(ToolUi.Tr("tools/rpg_object_editor/number_invalid"), ToolStatusKind.Error);

        private void OpenPicker(string titleKey, IEnumerable<string> ids, string current, Action<string> onPick)
        {
            var dialog = new RpgChoicePickerDialog(ToolUi.Tr(titleKey), ids, current);
            dialog.Picked += onPick;
            AddChild(dialog);
            dialog.PopupCentered(new Vector2I(520, 480));
        }

        // ── JSON helpers ───────────────────────────────────────────

        private static bool? SolidFlag(JsonObject target) => target?["Solid"] is JsonValue value && value.TryGetValue(out bool flag) ? flag : null;

        private static bool FlagOf(JsonObject target, string key) => target?[key] is JsonValue value && value.TryGetValue(out bool flag) && flag;

        private static float NumberOf(JsonObject target, string key, float fallback)
        {
            var numbers = RpgObjectNumbers.FromNode(target?[key]);
            return numbers is { Length: 1 } ? numbers[0] : fallback;
        }

        private static (int W, int H) FootprintOf(JsonObject root)
        {
            var size = RpgObjectNumbers.FromNode(root["Footprint"]?["Size"]);
            var width = size is { Length: > 0 } ? Math.Max(1, (int)size[0]) : 1;
            var height = size is { Length: > 1 } ? Math.Max(1, (int)size[1]) : 1;
            return (width, height);
        }

        /// <summary>The footprint mask: <c>true</c> where the cell blocks walking (the rows, or the object's solidity).</summary>
        private static bool[,] MaskOf(JsonObject root, int width, int height)
        {
            var rows = root["Footprint"]?["Solid"] as JsonArray;
            var fallback = SolidFlag(root) ?? FormOf(root) != "surface";
            var mask = new bool[width, height];
            for (var y = 0; y < height; y++)
            {
                var row = rows != null && y < rows.Count ? rows[y]?.ToString() : null;
                for (var x = 0; x < width; x++)
                    mask[x, y] = row != null && x < row.Length ? row[x] == '#' : fallback;
            }
            return mask;
        }

        private static bool TryInts(string text, out int[] values)
        {
            values = null;
            if (!RpgObjectNumbers.TryParse(text, out var numbers))
                return false;
            values = numbers?.Select(v => (int)Math.Round(v)).ToArray();
            return true;
        }

        private static bool TryPoints(string text, out JsonArray points)
        {
            points = null;
            if (string.IsNullOrWhiteSpace(text))
                return true;
            var result = new JsonArray();
            foreach (var group in text.Split(';', StringSplitOptions.RemoveEmptyEntries))
            {
                if (!RpgObjectNumbers.TryParse(group, out var values) || values is not { Length: 3 })
                    return false;
                result.Add(RpgObjectNumbers.ToNode(values));
            }
            points = result;
            return true;
        }

        private static string PointsText(JsonNode node)
            => node is JsonArray points ? string.Join("; ", points.Select(p => RpgObjectNumbers.Format(RpgObjectNumbers.FromNode(p)))) : string.Empty;

        private static JsonValue Text(string value) => JsonValue.Create(value);

        // ── preview choice and import ──────────────────────────────

        private void AddPreviewChoice(EntityRecord record)
        {
            var data = record.Data;
            var items = new List<(string Value, string Text)> { (string.Empty, ToolUi.Tr("tools/rpg_object_editor/show_default")) };
            if (data["Variants"] is JsonObject variants)
            {
                foreach (var name in variants.Select(p => p.Key))
                    items.Add(("variant:" + name, string.Format(ToolUi.Tr("tools/rpg_object_editor/show_variant"), name)));
            }
            if (data["States"] is JsonArray states)
            {
                for (var i = 0; i < states.Count; i++)
                    items.Add(("state:" + i, string.Format(ToolUi.Tr("tools/rpg_object_editor/show_state"), i + 1, Text(states[i] as JsonObject, "Id") ?? "-")));
            }
            PickField("tools/rpg_object_editor/show", "tools/rpg_object_editor/show_tip", items, previewChoice, v =>
            {
                previewChoice = v;
                RenderPreview(record);
            }, "PreviewChoice");
        }

        /// <summary>"Make a variant": a new own object that inherits this one (<see cref="AskVariantName"/>).</summary>
        private void AddMakeVariant()
        {
            AddRow(ButtonOf("tools/rpg_object_editor/make_variant", AskVariantName, "MakeVariant"));
            blocksHost.AddChild(ToolUi.Hint(ToolUi.Tr("tools/rpg_object_editor/make_variant_tip")));
        }

        private void AddImport(string form)
        {
            if (form is not ("sprite" or "surface" or "segment"))
                return;
            Section("tools/rpg_object_editor/section_picture");
            AddRow(ButtonOf("tools/rpg_object_editor/import", OpenImportDialog, "ImportPng"));
            blocksHost.AddChild(ToolUi.Hint(ToolUi.Tr("tools/rpg_object_editor/import_hint")));
        }

        // ── parts ──────────────────────────────────────────────────

        /// <summary>
        /// The parts of the object, a state or a variant (<paramref name="at"/> reaches it from the root). Each part is a
        /// small block: id, picture, geometry, along, crop, cells, points, projection, and the order buttons.
        /// </summary>
        private void AddParts(JsonObject target, Func<JsonObject, JsonObject> at, string prefix, bool inherit = false)
        {
            Section("tools/rpg_object_editor/section_parts");
            if (inherit)
                AddInheritedParts(target);
            var parts = target?["Parts"] as JsonArray ?? new JsonArray();
            for (var i = 0; i < parts.Count; i++)
                AddPart(parts[i] as JsonObject ?? new JsonObject(), i, parts.Count, at, prefix);
            AddRow(ButtonOf("tools/rpg_object_editor/part_add", () => Edit(root =>
            {
                var list = Arr(at(root), "Parts");
                list.Add(new JsonObject { ["Id"] = "part" + (list.Count + 1) });
            }), prefix + "_Add"));
        }

        private void AddPart(JsonObject part, int index, int count, Func<JsonObject, JsonObject> at, string prefix)
        {
            var name = prefix + index;
            blocksHost.AddChild(new Label { Text = string.Format(ToolUi.Tr("tools/rpg_object_editor/part_title"), index + 1) });

            // An empty text removes the property (the projection, the order, the picture are optional).
            void SetText(string key, string value)
                => Edit(root =>
                {
                    var part = Rebind(Arr(at(root), "Parts"), index);
                    if (value.Length == 0)
                        part.Remove(key);
                    else
                        part[key] = JsonValue.Create(value);
                });

            TextField("tools/rpg_object_editor/part_id", null, Text(part, "Id")?.ToString(), v => SetText("Id", v), name + "_Id");
            PictureField("tools/rpg_object_editor/part_image", "tools/rpg_object_editor/part_image_tip", Text(part, "Image")?.ToString(),
                RpgTextures.ImageGroup, v => SetText("Image", v), name + "_Image");
            PickField("tools/rpg_object_editor/geometry", "tools/rpg_object_editor/geometry_tip",
                Geometries.Select(g => (g, g)).ToList(), Text(part, "Geometry")?.ToString() ?? "upright",
                v => SetText("Geometry", v), name + "_Geometry");
            PickField("tools/rpg_object_editor/along", "tools/rpg_object_editor/along_tip",
                Alongs.Select(g => (g, g)).ToList(), Text(part, "Along")?.ToString() ?? "auto",
                v => SetText("Along", v), name + "_Along");
            TextField("tools/rpg_object_editor/crop", "tools/rpg_object_editor/crop_tip", RpgObjectNumbers.Format(RpgObjectNumbers.FromNode(part["Crop"])),
                v => Ints(v, "Crop", at, index), name + "_Crop");
            TextField("tools/rpg_object_editor/part_at", "tools/rpg_object_editor/part_at_tip", RpgObjectNumbers.Format(RpgObjectNumbers.FromNode(part["At"])),
                v => Numbers(v, "At", at, index), name + "_At");
            TextField("tools/rpg_object_editor/part_size", "tools/rpg_object_editor/part_size_tip", RpgObjectNumbers.Format(RpgObjectNumbers.FromNode(part["Size"])),
                v => Numbers(v, "Size", at, index), name + "_Size");
            TextField("tools/rpg_object_editor/part_points", "tools/rpg_object_editor/part_points_tip", PointsText(part["Points"]),
                v =>
                {
                    if (!TryPoints(v, out var points))
                    {
                        Invalid();
                        return;
                    }
                    Edit(root => Rebind(Arr(at(root), "Parts"), index)["Points"] = points);
                }, name + "_Points");
            PickField("tools/rpg_object_editor/part_only", "tools/rpg_object_editor/part_only_tip",
                new[] { (string.Empty, ToolUi.Tr("tools/rpg_object_editor/part_only_any")), ("topdown", "topdown"), ("isometric", "isometric") },
                Text(part, "Only")?.ToString() ?? string.Empty, v => SetText("Only", v), name + "_Only");
            PickField("tools/rpg_object_editor/draw_order", "tools/rpg_object_editor/draw_order_tip",
                new[] { (string.Empty, ToolUi.Tr("tools/rpg_object_editor/draw_order_default")) }.Concat(DrawOrders.Select(d => (d, d))).ToList(),
                Text(part, "DrawOrder")?.ToString() ?? string.Empty, v => SetText("DrawOrder", v), name + "_DrawOrder");
            TextField("tools/rpg_object_editor/part_sort_offset", null, RpgObjectNumbers.Format(RpgObjectNumbers.FromNode(part["SortOffset"])),
                v => Numbers(v, "SortOffset", at, index), name + "_SortOffset");
            AddRow(
                ButtonOf("tools/rpg_object_editor/part_up", () => Edit(root => Swap(Arr(at(root), "Parts"), index, index - 1)), name + "_Up"),
                ButtonOf("tools/rpg_object_editor/part_down", () => Edit(root => Swap(Arr(at(root), "Parts"), index, index + 1)), name + "_Down"),
                ButtonOf("tools/rpg_object_editor/part_remove", () => Edit(root => Arr(at(root), "Parts").RemoveAt(index)), name + "_Remove"));
        }

        /// <summary>The part at <paramref name="index"/>, created when the list is shorter.</summary>
        private static JsonObject Rebind(JsonArray list, int index) => At(list, index);

        private static void Swap(JsonArray list, int a, int b)
        {
            if (a < 0 || b < 0 || a >= list.Count || b >= list.Count)
                return;
            (list[a], list[b]) = (list[b], list[a]);
        }

        private void Ints(string text, string key, Func<JsonObject, JsonObject> at, int index)
        {
            if (!TryInts(text, out var values))
            {
                Invalid();
                return;
            }
            Edit(root => Rebind(Arr(at(root), "Parts"), index)[key] = values == null ? null : RpgObjectNumbers.ToIntNode(values));
        }

        private void Numbers(string text, string key, Func<JsonObject, JsonObject> at, int index)
        {
            if (!RpgObjectNumbers.TryParse(text, out var values))
            {
                Invalid();
                return;
            }
            Edit(root => Rebind(Arr(at(root), "Parts"), index)[key] = RpgObjectNumbers.ToNode(values));
        }

        // ── footprint ──────────────────────────────────────────────

        private void AddFootprint(JsonObject data)
        {
            Section("tools/rpg_object_editor/section_footprint");
            var (width, height) = FootprintOf(data);
            SpinField("tools/rpg_object_editor/footprint_width", "tools/rpg_object_editor/footprint_tip", width, 1, 12, 1,
                v => Edit(root =>
                {
                    EnsureOwnFootprint(root);
                    ResizeFootprint(root, (int)v, FootprintOf(root).H);
                }), "FootprintWidth");
            SpinField("tools/rpg_object_editor/footprint_height", "tools/rpg_object_editor/footprint_tip", height, 1, 12, 1,
                v => Edit(root =>
                {
                    EnsureOwnFootprint(root);
                    ResizeFootprint(root, FootprintOf(root).W, (int)v);
                }), "FootprintHeight");

            var mask = MaskOf(data, width, height);
            var grid = new GridContainer { Columns = width, Name = "FootprintMask" };
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    var cellX = x;
                    var cellY = y;
                    var cell = new Button
                    {
                        Text = mask[x, y] ? "#" : ".",
                        CustomMinimumSize = new Vector2(30, 30),
                        Name = $"Mask_{x}_{y}",
                        TooltipText = ToolUi.Tr("tools/rpg_object_editor/footprint_cell_tip"),
                    };
                    cell.Pressed += () => Edit(root =>
                    {
                        EnsureOwnFootprint(root);
                        PaintFootprint(root, cellX, cellY);
                    });
                    grid.AddChild(cell);
                }
            }
            blocksHost.AddChild(grid);
            blocksHost.AddChild(ToolUi.Hint(ToolUi.Tr("tools/rpg_object_editor/footprint_mask_hint")));
        }

        /// <summary>A new size drops the mask rows: they are written again from the object's solidity.</summary>
        private static void ResizeFootprint(JsonObject root, int width, int height)
        {
            var footprint = Obj(root, "Footprint");
            footprint["Size"] = RpgObjectNumbers.ToIntNode(new[] { width, height });
            footprint.Remove("Solid");
        }

        // ── flags ──────────────────────────────────────────────────

        /// <summary>Solidity, hidden, and for the object: the form's own flags, draw order.</summary>
        private void AddFlags(JsonObject target, string form, Func<JsonObject, JsonObject> at, bool isObject)
        {
            Section("tools/rpg_object_editor/section_flags");
            var solid = SolidFlag(target);
            PickField("tools/rpg_object_editor/solid", "tools/rpg_object_editor/solid_tip",
                new[] { (string.Empty, ToolUi.Tr("tools/rpg_object_editor/solid_default")), ("yes", ToolUi.Tr("tools/rpg_object_editor/solid_yes")), ("no", ToolUi.Tr("tools/rpg_object_editor/solid_no")) },
                solid == null ? string.Empty : solid.Value ? "yes" : "no",
                v => Edit(root => Set(at(root), "Solid", v.Length == 0 ? null : JsonValue.Create(v == "yes"))), "Solid");
            CheckField("tools/rpg_object_editor/hidden", "tools/rpg_object_editor/hidden_tip", FlagOf(target, "Hidden"),
                on => Edit(root => Set(at(root), "Hidden", on ? JsonValue.Create(true) : null)), "Hidden");
            if (!isObject)
                return;
            if (form == "surface")
            {
                CheckField("tools/rpg_object_editor/water", "tools/rpg_object_editor/water_tip", FlagOf(target, "Water"),
                    on => Edit(root => Set(root, "Water", on ? JsonValue.Create(true) : null)), "Water");
                CheckField("tools/rpg_object_editor/diffuse_on", "tools/rpg_object_editor/diffuse_on_tip",
                    target["Surface"]?["Diffuse"] is not JsonValue { } diffuse || !diffuse.TryGetValue(out bool on) || on,
                    on => Edit(root => Obj(root, "Surface")["Diffuse"] = JsonValue.Create(on)), "Diffuse");
            }
            if (form == "segment")
            {
                SpinField("tools/rpg_object_editor/segment_height", "tools/rpg_object_editor/segment_height_tip",
                    NumberOf(target["Segment"] as JsonObject, "Height", 1f), 0.5, 10, 0.5,
                    v => Edit(root => Obj(root, "Segment")["Height"] = JsonValue.Create(v)), "SegmentHeight");
            }
            if (form is "sprite" or "actor")
            {
                var order = target["DrawOrder"]?.ToString() ?? string.Empty;
                PickField("tools/rpg_object_editor/draw_order", "tools/rpg_object_editor/draw_order_tip",
                    new[] { (string.Empty, ToolUi.Tr("tools/rpg_object_editor/draw_order_default")) }.Concat(DrawOrders.Select(d => (d, d))).ToList(),
                    order, v => Edit(root => Set(root, "DrawOrder", v.Length == 0 ? null : JsonValue.Create(v))), "DrawOrder");
            }
        }

        // ── interaction ────────────────────────────────────────────

        private void AddInteract(JsonObject target, Func<JsonObject, JsonObject> at, string tag)
        {
            Section("tools/rpg_object_editor/section_interact");
            var interact = target["Interact"] as JsonObject;
            if (interact == null)
            {
                AddRow(ButtonOf("tools/rpg_object_editor/interact_add", () => Edit(root => Obj(at(root), "Interact")), tag + "_InteractAdd"));
                return;
            }

            PickField("tools/rpg_object_editor/trigger", "tools/rpg_object_editor/trigger_tip",
                new[] { ("Use", "Use"), ("Step", "Step") }, Text(interact, "Trigger")?.ToString() ?? "Use",
                v => Edit(root => Obj(at(root), "Interact")["Trigger"] = JsonValue.Create(v)), tag + "_Trigger");

            var scenario = Text(interact, "Scenario")?.ToString();
            var scenarioLabel = new Label { Text = scenario ?? ToolUi.Tr("tools/rpg_object_editor/scenario_none"), Name = tag + "_ScenarioLabel", AutowrapMode = TextServer.AutowrapMode.Arbitrary };
            blocksHost.AddChild(new Label { Text = ToolUi.Tr("tools/rpg_object_editor/scenario") });
            AddRow(scenarioLabel, ButtonOf("tools/rpg_object_editor/scenario_pick", () => OpenPicker("tools/rpg_object_editor/dialog_scenario", ScenarioIds(), scenario,
                id => Edit(root =>
                {
                    var block = Obj(at(root), "Interact");
                    block["Scenario"] = id;
                    block.Remove("Teleport");
                })), tag + "_ScenarioPick"));

            var commands = interact["Commands"] as JsonArray;
            var lines = new TextEdit { Text = commands == null ? string.Empty : string.Join("\n", commands.Select(c => c?.ToString())), CustomMinimumSize = new Vector2(0, 70), Name = tag + "_Commands" };
            lines.FocusExited += () =>
            {
                if (building)
                    return;
                var typed = lines.Text.Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0).ToList();
                Edit(root =>
                {
                    var block = Obj(at(root), "Interact");
                    block.Remove("Teleport");
                    block["Commands"] = typed.Count == 0 ? null : new JsonArray(typed.Select(l => (JsonNode)JsonValue.Create(l)).ToArray());
                    if (typed.Count == 0)
                        block.Remove("Commands");
                });
            };
            ToolUi.Field(blocksHost, "tools/rpg_object_editor/commands", lines, "tools/rpg_object_editor/commands_tip");

            var teleport = interact["Teleport"] as JsonObject;
            var mapLabel = new Label { Text = Text(teleport, "Map")?.ToString() ?? ToolUi.Tr("tools/rpg_object_editor/map_none"), Name = tag + "_MapLabel" };
            blocksHost.AddChild(new Label { Text = ToolUi.Tr("tools/rpg_object_editor/teleport") });
            var mapId = Text(teleport, "Map")?.ToString();
            AddRow(mapLabel, ButtonOf("tools/rpg_object_editor/map_pick", () => OpenPicker("tools/rpg_object_editor/dialog_map", MapIds(), mapId,
                id => Edit(root =>
                {
                    var block = Obj(at(root), "Interact");
                    block.Remove("Scenario");
                    block.Remove("Commands");
                    var jump = Obj(block, "Teleport");
                    jump["Map"] = id;
                    jump["X"] ??= 0;
                    jump["Y"] ??= 0;
                    jump["Dir"] ??= "Down";
                })), tag + "_MapPick"));
            if (teleport != null)
            {
                SpinField("tools/rpg_object_editor/tele_x", "tools/rpg_object_editor/tele_tip", NumberOf(teleport, "X", 0), 0, 999, 1,
                    v => Edit(root => Obj(Obj(at(root), "Interact"), "Teleport")["X"] = JsonValue.Create((int)v)), tag + "_TeleX");
                SpinField("tools/rpg_object_editor/tele_y", "tools/rpg_object_editor/tele_tip", NumberOf(teleport, "Y", 0), 0, 999, 1,
                    v => Edit(root => Obj(Obj(at(root), "Interact"), "Teleport")["Y"] = JsonValue.Create((int)v)), tag + "_TeleY");
                PickField("tools/rpg_object_editor/tele_dir", null, Directions.Select(d => (d, d)).ToList(), Text(teleport, "Dir")?.ToString() ?? "Down",
                    v => Edit(root => Obj(Obj(at(root), "Interact"), "Teleport")["Dir"] = JsonValue.Create(v)), tag + "_TeleDir");
                AddRow(ButtonOf("tools/rpg_object_editor/teleport_remove", () => Edit(root => Obj(at(root), "Interact").Remove("Teleport")), tag + "_TeleRemove"));
            }

            AddRow(ButtonOf("tools/rpg_object_editor/interact_clear", () => Edit(root => at(root).Remove("Interact")), tag + "_InteractClear"));
        }

        /// <summary>The scenarios any loaded mod offers, as the Scenario field names them (no extension).</summary>
        private static IEnumerable<string> ScenarioIds()
            => Files.Collect("scenarios").Keys
                .Where(k => k.EndsWith(".scenario", StringComparison.OrdinalIgnoreCase))
                .Select(k => k.Substring(0, k.Length - ".scenario".Length))
                .OrderBy(k => k, StringComparer.Ordinal);

        /// <summary>The maps the game knows, by id.</summary>
        private static IEnumerable<string> MapIds()
            => Entities.Collect<RpgMap>().Keys.OrderBy(k => k, StringComparer.Ordinal);

        // ── states ─────────────────────────────────────────────────

        private void AddStates(JsonObject data, string form)
        {
            Section("tools/rpg_object_editor/section_states");
            var states = data["States"] as JsonArray ?? new JsonArray();
            stateSel = states.Count == 0 ? -1 : Math.Clamp(stateSel, 0, states.Count - 1);
            var stateList = ListOf("StateList", Enumerable.Range(0, states.Count).Select(i => string.Format(ToolUi.Tr("tools/rpg_object_editor/state_item"), i + 1, Text(states[i] as JsonObject, "Id")?.ToString() ?? "-")), stateSel);
            stateList.ItemSelected += i =>
            {
                stateSel = (int)i;
                Refresh();
            };
            AddRow(
                ButtonOf("tools/rpg_object_editor/state_add", () => Edit(root =>
                {
                    var list = Arr(root, "States");
                    list.Add(new JsonObject { ["Id"] = "state" + (list.Count + 1) });
                    stateSel = list.Count - 1;
                }), "StateAdd"),
                ButtonOf("tools/rpg_object_editor/state_remove", () => Edit(root =>
                {
                    var list = Arr(root, "States");
                    if (stateSel >= 0 && stateSel < list.Count)
                        list.RemoveAt(stateSel);
                    stateSel = Math.Max(0, stateSel - 1);
                }), "StateRemove"),
                ButtonOf("tools/rpg_object_editor/state_up", () => Edit(root =>
                {
                    Swap(Arr(root, "States"), stateSel, stateSel - 1);
                    stateSel--;
                }), "StateUp"),
                ButtonOf("tools/rpg_object_editor/state_down", () => Edit(root =>
                {
                    Swap(Arr(root, "States"), stateSel, stateSel + 1);
                    stateSel++;
                }), "StateDown"));
            if (stateSel < 0)
                return;

            var index = stateSel;
            Func<JsonObject, JsonObject> at = root => At(Arr(root, "States"), index);
            var state = states[index] as JsonObject ?? new JsonObject();
            TextField("tools/rpg_object_editor/state_id", null, Text(state, "Id")?.ToString(), v => Edit(root => Set(at(root), "Id", Text(v))), "StateId");
            TextField("tools/rpg_object_editor/state_when", "tools/rpg_object_editor/state_when_tip", Text(state, "When")?.ToString(), v => Edit(root => Set(at(root), "When", Text(v))), "StateWhen");
            PickField("tools/rpg_object_editor/facing", null, new[] { (string.Empty, ToolUi.Tr("tools/rpg_object_editor/facing_keep")) }.Concat(Directions.Select(d => (d, d))).ToList(),
                Text(state, "Facing")?.ToString() ?? string.Empty, v => Edit(root => Set(at(root), "Facing", v.Length == 0 ? null : Text(v))), "StateFacing");
            AddFlags(state, form, at, false);
            AddParts(state, at, "State" + index + "_Part");
            AddInteract(state, at, "State" + index);
        }

        // ── variants ───────────────────────────────────────────────

        private void AddVariants(JsonObject data, string form)
        {
            Section("tools/rpg_object_editor/section_variants");
            var variants = data["Variants"] as JsonObject;
            var names = variants?.Select(p => p.Key).ToList() ?? new List<string>();
            var selected = names.IndexOf(variantSel ?? string.Empty);
            if (names.Count > 0 && selected < 0)
                selected = 0;
            variantSel = selected >= 0 ? names[selected] : null;
            var variantList = ListOf("VariantList", names, selected);
            variantList.ItemSelected += i =>
            {
                variantSel = names[(int)i];
                Refresh();
            };

            var nameEdit = new LineEdit { PlaceholderText = ToolUi.Tr("tools/rpg_object_editor/variant_name"), SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, Name = "VariantName" };
            AddRow(
                nameEdit,
                ButtonOf("tools/rpg_object_editor/variant_add", () =>
                {
                    var typed = nameEdit.Text.Trim();
                    if (typed.Length == 0 || names.Contains(typed))
                    {
                        Invalid();
                        return;
                    }
                    variantSel = typed;
                    Edit(root => Obj(root, "Variants")[typed] = new JsonObject());
                }, "VariantAdd"),
                ButtonOf("tools/rpg_object_editor/variant_remove", () =>
                {
                    if (variantSel == null)
                        return;
                    var removed = variantSel;
                    variantSel = null;
                    Edit(root => (root["Variants"] as JsonObject)?.Remove(removed));
                }, "VariantRemove"));
            if (variantSel == null)
                return;

            var name = variantSel;
            Func<JsonObject, JsonObject> at = root => Obj(Obj(root, "Variants"), name);
            var variant = variants[name] as JsonObject ?? new JsonObject();
            if (form == "sprite")
            {
                AddParts(variant, at, "Variant" + name + "_Part");
                return;
            }
            var key = form == "surface" ? "Surface" : "Segment";
            if (form is "surface" or "segment")
            {
                PictureField("tools/rpg_object_editor/variant_picture", "tools/rpg_object_editor/variant_picture_tip", Text(variant[key] as JsonObject, "Image")?.ToString(),
                    RpgTextures.ImageGroup, v => Edit(root => Obj(Obj(Obj(root, "Variants"), name), key)["Image"] = Text(v)), "VariantPicture");
            }
        }

        // ── lights ─────────────────────────────────────────────────

        private void AddLights(JsonObject data)
        {
            Section("tools/rpg_object_editor/section_lights");
            var main = data["Light"] as JsonObject;
            CheckField("tools/rpg_object_editor/light_main", "tools/rpg_object_editor/light_main_tip", main != null,
                on => Edit(root => Set(root, "Light", on ? new JsonObject { ["Radius"] = 3.0, ["Color"] = "#ffd8a0" } : null)), "LightMain");
            if (main != null)
                AddLight(main, WholeObject, "LightMain");

            var lights = data["Lights"] as JsonArray ?? new JsonArray();
            lightSel = lights.Count == 0 ? -1 : Math.Clamp(lightSel, 0, lights.Count - 1);
            var lightList = ListOf("LightList", Enumerable.Range(0, lights.Count).Select(i => string.Format(ToolUi.Tr("tools/rpg_object_editor/light_item"), i + 1)), lightSel);
            lightList.ItemSelected += i =>
            {
                lightSel = (int)i;
                Refresh();
            };
            AddRow(
                ButtonOf("tools/rpg_object_editor/light_add", () => Edit(root =>
                {
                    var list = Arr(root, "Lights");
                    list.Add(new JsonObject { ["Radius"] = 3.0, ["Color"] = "#ffd8a0" });
                    lightSel = list.Count - 1;
                }), "LightAdd"),
                ButtonOf("tools/rpg_object_editor/light_remove", () => Edit(root =>
                {
                    var list = Arr(root, "Lights");
                    if (lightSel >= 0 && lightSel < list.Count)
                        list.RemoveAt(lightSel);
                    lightSel = Math.Max(0, lightSel - 1);
                }), "LightRemove"));
            if (lightSel < 0)
                return;
            var index = lightSel;
            AddLight(lights[index] as JsonObject ?? new JsonObject(), root => At(Arr(root, "Lights"), index), "LightExtra" + index);
        }

        private void AddLight(JsonObject light, Func<JsonObject, JsonObject> at, string tag)
        {
            SpinField("tools/rpg_object_editor/light_radius", "tools/rpg_object_editor/light_radius_tip", NumberOf(light, "Radius", 3f), 0.5, 50, 0.5,
                v => Edit(root => Set(at(root), "Radius", JsonValue.Create(v))), tag + "_Radius");
            var hex = Text(light, "Color")?.ToString() ?? "#ffd8a0";
            var colour = new ColorPickerButton { Color = Color.FromHtml(hex), CustomMinimumSize = new Vector2(96, 28), Name = tag + "_Color" };
            colour.ColorChanged += c =>
            {
                if (building)
                    return;
                var text = "#" + c.ToHtml(hex.Length == 9);
                Edit(root => Set(at(root), "Color", JsonValue.Create(text)));
            };
            ToolUi.Field(blocksHost, "tools/rpg_object_editor/light_color", colour, "tools/rpg_object_editor/light_color_tip");
            TextField("tools/rpg_object_editor/light_at", "tools/rpg_object_editor/light_at_tip", RpgObjectNumbers.Format(RpgObjectNumbers.FromNode(light["At"])),
                v =>
                {
                    if (!RpgObjectNumbers.TryParse(v, out var values))
                    {
                        Invalid();
                        return;
                    }
                    Edit(root => Set(at(root), "At", RpgObjectNumbers.ToNode(values)));
                }, tag + "_At");
        }

        // ── route ──────────────────────────────────────────────────

        private void AddRoute(JsonObject data)
        {
            Section("tools/rpg_object_editor/section_route");
            var route = data["Route"] as JsonObject;
            CheckField("tools/rpg_object_editor/route_on", "tools/rpg_object_editor/route_on_tip", route != null,
                on => Edit(root => Set(root, "Route", on ? new JsonObject { ["Steps"] = new JsonArray(), ["Every"] = 1.0, ["Repeat"] = true } : null)), "RouteOn");
            if (route == null)
                return;
            var steps = (route["Steps"] as JsonArray)?.Select(n => n?.ToString()).Where(s => !string.IsNullOrEmpty(s)) ?? Enumerable.Empty<string>();
            TextField("tools/rpg_object_editor/route_steps", "tools/rpg_object_editor/route_steps_tip", string.Join(", ", steps),
                v =>
                {
                    var parts = v.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
                    Edit(root => Obj(root, "Route")["Steps"] = new JsonArray(parts.Select(p => (JsonNode)JsonValue.Create(p)).ToArray()));
                }, "RouteSteps");
            SpinField("tools/rpg_object_editor/route_every", "tools/rpg_object_editor/route_every_tip", NumberOf(route, "Every", 1f), 0.1, 60, 0.1,
                v => Edit(root => Obj(root, "Route")["Every"] = JsonValue.Create(v)), "RouteEvery");
            CheckField("tools/rpg_object_editor/route_repeat", "tools/rpg_object_editor/route_repeat_tip", route["Repeat"] is not JsonValue { } r || !r.TryGetValue(out bool on) || on,
                v => Edit(root => Obj(root, "Route")["Repeat"] = JsonValue.Create(v)), "RouteRepeat");
        }
    }
}
