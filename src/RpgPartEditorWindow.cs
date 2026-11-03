namespace Cthangover.Tools.RpgObjectEditor
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text.Json;
    using System.Text.Json.Nodes;
    using Cthangover.Core.Mods.Resolvers;
    using Cthangover.Core.Utils;
    using Cthangover.Mods.Interface.Tool;
    using Cthangover.Mods.Interface.Theming;
    using Cthangover.Mods.Rpg;
    using Cthangover.Mods.Rpg.Model;
    using Cthangover.Mods.TileRender;
    using Cthangover.Tools.Common;
    using Cthangover.Tools.EntityEditor;
    using Godot;

    /// <summary>Map-editor layout: permanent layers list, large canvas, explicit selection/depth/player tools and an inspector.</summary>
    public partial class RpgPartEditorWindow : ToolWindow
    {
        public RpgObjectPreview Canvas { get; }
        private readonly ItemList parts;
        private readonly OptionButton order;
        private readonly Dictionary<string, RpgObject> objects;
        private bool refreshing;
        private readonly Label selectionHint;
        private readonly Label layerName;
        private readonly Label depthStatus;
        private readonly Button move;
        private readonly Button depth;
        private readonly Button player;
        public event Action<JsonArray> Applied;
        public RpgPartEditorWindow(RpgObject sample) : base("tools/rpg_object_editor/visual_parts")
        {
            Name = "VisualPartsWindow"; objects = Entities.Collect<RpgObject>();
            var column = CreateFillContainer(); AddChild(column);
            var toolbar = new HFlowContainer(); column.AddChild(toolbar);
            var group = new ButtonGroup();
            move = ModeButton(toolbar, "MovePart", "tools/rpg_object_editor/v2_select", group, OnMove);
            depth = ModeButton(toolbar, "DepthPart", "tools/rpg_object_editor/visual_sort", group, OnDepth);
            player = ModeButton(toolbar, "MovePlayer", "tools/rpg_object_editor/v2_player", group, OnPlayer);
            move.SetPressedNoSignal(true);
            toolbar.AddChild(ToolUi.Button("tools/entity_editor/undo", Undo));
            toolbar.AddChild(ToolUi.Button("tools/entity_editor/redo", Redo));
            toolbar.AddChild(ToolUi.Button("tools/rpg_object_editor/visual_fit", Fit));
            var body = new HSplitContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill }; column.AddChild(body);
            var layers = new VBoxContainer { CustomMinimumSize = new Vector2(240, 0) };
            layers.AddChild(ToolUi.Section("tools/rpg_object_editor/v2_layers"));
            parts = new ItemList { Name = "Parts", SizeFlagsVertical = Control.SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(240, 0),
                IconMode = ItemList.IconModeEnum.Left, FixedIconSize = new Vector2I(48, 48) };
            layers.AddChild(parts); parts.ItemSelected += OnPartPicked; parts.ItemActivated += OnPartActivated;
            layers.AddChild(ToolUi.Button("tools/rpg_object_editor/v2_add", PickImage));
            var arrows = new HBoxContainer(); layers.AddChild(arrows);
            arrows.AddChild(ToolUi.Button("tools/rpg_object_editor/part_up", Up));
            arrows.AddChild(ToolUi.Button("tools/rpg_object_editor/part_down", Down));
            layers.AddChild(ToolUi.Button("tools/rpg_object_editor/part_remove", Remove)); body.AddChild(layers);
            var centre = new HSplitContainer(); body.AddChild(centre);
            Canvas = new RpgObjectPreview { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, SizeFlagsVertical = Control.SizeFlags.ExpandFill };
            centre.AddChild(Canvas); Canvas.EnablePartEditing(); Canvas.PartsChanged += RefreshParts; Canvas.ProbeChanged += RefreshStatus;
            var settings = new VBoxContainer { CustomMinimumSize = new Vector2(270, 0) }; centre.AddChild(settings);
            settings.AddChild(ToolUi.Section("tools/rpg_object_editor/v2_settings"));
            layerName = EditorHint(""); settings.AddChild(layerName);
            order = new OptionButton { Name = "PartOrder" };
            order.AddItem(ToolUi.Tr("tools/rpg_object_editor/visual_below"));
            order.AddItem(ToolUi.Tr("tools/rpg_object_editor/visual_depth"));
            order.AddItem(ToolUi.Tr("tools/rpg_object_editor/visual_above"));
            ToolUi.Field(settings, "tools/rpg_object_editor/draw_order", order);
            order.ItemSelected += OnOrderPicked;
            settings.AddChild(ToolUi.Button("tools/rpg_object_editor/v2_auto_depth", AutoDepth));
            settings.AddChild(ToolUi.Button("tools/rpg_object_editor/v2_focus", FocusPart));
            selectionHint = EditorHint(""); settings.AddChild(selectionHint);
            settings.AddChild(ToolUi.Section("tools/rpg_object_editor/v2_test"));
            settings.AddChild(EditorHint(ToolUi.Tr("tools/rpg_object_editor/v2_player_hint")));
            settings.AddChild(ToolUi.Button("tools/rpg_object_editor/v2_put_behind", Behind));
            settings.AddChild(ToolUi.Button("tools/rpg_object_editor/v2_put_front", Front));
            depthStatus = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart, Modulate = Colors.White }; settings.AddChild(depthStatus);
            settings.AddChild(EditorHint(ToolUi.Tr("tools/rpg_object_editor/v2_scale")));
            Canvas.Show(JsonSerializer.Deserialize<RpgObject>(JsonSerializer.Serialize(sample, JsonOptionsProvider.ReadOptions), JsonOptionsProvider.ReadOptions),
                Array.Empty<string>(), FindObject, RpgPlayerLook.LeaderCharset()); Canvas.SelectProjection("isometric");
            var footer = new HBoxContainer(); column.AddChild(footer);
            footer.AddChild(ToolUi.Button("tools/rpg_object_editor/visual_apply", Apply, ButtonKind.Primary));
            footer.AddChild(ToolUi.Button("tools/rpg_object_editor/visual_cancel", Cancel));
            RefreshParts();
        }
        private static Label EditorHint(string text)
        {
            var label = ToolUi.Hint(text);
            label.AddThemeColorOverride("font_color", new Color(0.82f, 0.84f, 0.88f));
            return label;
        }
        private static Button ModeButton(Node row, string name, string key, ButtonGroup group, Action handler)
        {
            var button = ToolUi.Button(key, handler); button.Name = name; button.ToggleMode = true; button.ButtonGroup = group; row.AddChild(button); return button;
        }
        private RpgObject FindObject(string id) => objects.TryGetValue(id, out var obj) ? obj : null;
        private void RefreshParts()
        {
            refreshing = true; parts.Clear();
            var list = Canvas.EditableParts;
            for (var i = 0; i < list.Count; i++)
            {
                var name = list[i].Image ?? list[i].Id ?? "…";
                parts.AddItem((i + 1) + ". " + Path.GetFileName(name)); parts.SetItemTooltip(i, name);
            }
            if (Canvas.SelectedPart >= 0 && Canvas.SelectedPart < parts.ItemCount)
            {
                parts.Select(Canvas.SelectedPart); layerName.Text = list[Canvas.SelectedPart].Image ?? "";
                var value = list[Canvas.SelectedPart].DrawOrder ?? TileDrawOrder.ByPosition;
                order.Select(value == TileDrawOrder.BelowCharacters ? 0 : value == TileDrawOrder.AboveCharacters ? 2 : 1);
                selectionHint.Text = list[Canvas.SelectedPart].Geometry == RpgPartGeometry.Plane ? ToolUi.Tr("tools/rpg_object_editor/visual_plane")
                    : ToolUi.Tr(value == TileDrawOrder.ByPosition ? "tools/rpg_object_editor/v2_depth_hint"
                        : value == TileDrawOrder.AboveCharacters ? "tools/rpg_object_editor/v2_always_front" : "tools/rpg_object_editor/v2_always_back");
            }
            refreshing = false; RefreshStatus();
        }
        private void RefreshStatus()
        {
            if (depthStatus == null || Canvas.Projection == RpgObjectPreview.Both) return;
            depthStatus.Text = ToolUi.Tr(Canvas.ProbeIsInFront(Canvas.Projection) ? "tools/rpg_object_editor/v2_front_status" : "tools/rpg_object_editor/v2_behind_status");
        }
        private void OnPartPicked(long index) { if (!refreshing) Canvas.SelectPart((int)index); }
        private void OnPartActivated(long index) { Canvas.SelectPart((int)index); Canvas.FocusSelectedPart(); }
        private void FocusPart() => Canvas.FocusSelectedPart();
        private void OnMove() => Canvas.PartMode = 0;
        private void OnDepth() => Canvas.PartMode = 1;
        private void OnPlayer() => Canvas.PartMode = 2;
        private void OnOrderPicked(long index)
        {
            if (refreshing) return;
            if (index == 1 && Canvas.SelectedPart < Canvas.EditableParts.Count && Canvas.EditableParts[Canvas.SelectedPart].SortOffset == null) Canvas.AutoDepthPoint();
            else Canvas.SetPartOrder(index == 0 ? TileDrawOrder.BelowCharacters : index == 2 ? TileDrawOrder.AboveCharacters : TileDrawOrder.ByPosition);
        }
        private void PickImage()
        {
            var picker = new PicturePickerDialog(RpgTextures.ImageGroup, ""); picker.Picked += OnImagePicked; AddChild(picker); picker.PopupCentered();
        }
        private void OnImagePicked(string image) => Canvas.AddOverlayPart(image);
        private void AutoDepth() => Canvas.AutoDepthPoint();
        private void Behind() => Canvas.PlaceProbeSide(false);
        private void Front() => Canvas.PlaceProbeSide(true);
        private void Up() => Canvas.MovePartOrder(-1);
        private void Down() => Canvas.MovePartOrder(1);
        private void Remove() => Canvas.RemoveSelectedPart();
        private void Undo() => Canvas.UndoParts();
        private void Redo() => Canvas.RedoParts();
        private void Fit() => Canvas.FitParts();
        public void Apply()
        {
            Applied?.Invoke(JsonNode.Parse(JsonSerializer.Serialize(Canvas.EditableParts, JsonOptionsProvider.ReadOptions)).AsArray()); QueueFree();
        }
        private void Cancel() => QueueFree();
        protected override bool HandleKey(InputEventKey key)
        {
            if (key.CtrlPressed && key.Keycode == Key.Z) { Canvas.UndoParts(); return true; }
            if (key.CtrlPressed && key.Keycode == Key.Y) { Canvas.RedoParts(); return true; }
            if (key.Keycode == Key.Escape) { Canvas.CancelPartDrag(); return true; }
            return false;
        }
    }
}
