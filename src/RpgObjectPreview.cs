namespace Cthangover.Tools.RpgObjectEditor
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Cthangover.Mods.Rpg;
    using Cthangover.Mods.Rpg.Model;
    using Cthangover.Mods.Rpg.Render;
    using Cthangover.Mods.TileRender;
    using Cthangover.Tools.Common;
    using Godot;

    /// <summary>
    /// The object drawn on its sample map (<see cref="RpgObjectSample"/>) in the top-down and the
    /// isometric projection, by the game's own scene builder and renderers. A projection toggle shows
    /// one of them or both. Nothing is simulated: the scene is built once per shown object.
    /// <para/>
    /// Context on the preview: a dropdown picks the surface material under and around the object (default
    /// grass), and a sprite has two player figures next to it, one behind and one in front, to show the scale
    /// and the depth order. The brush decides what a press on the preview does: for a sprite the anchor is a
    /// handle (a drag moves it, <see cref="AnchorDragged"/>), or the footprint brush (a press on a cell reports
    /// it through <see cref="FootprintClicked"/>; the window toggles the cell's blocking or extends the footprint).
    /// </summary>
    public partial class RpgObjectPreview : VBoxContainer
    {
        /// <summary>Toggle value: both projections side by side.</summary>
        public const string Both = "both";

        /// <summary>Brush value: a drag moves the sprite's anchor.</summary>
        public const string AnchorBrush = "anchor";

        /// <summary>Brush value: a press on a cell of a sprite's footprint.</summary>
        public const string FootprintBrush = "footprint";

        /// <summary>Surface material the ground around the object shows when the project does not say otherwise.</summary>
        public const string DefaultNeighbour = "village_pack/ground/grass";

        private const string TopDown = "topdown";
        private const string Isometric = "isometric";
        private const float CanvasHeight = 240f;

        private readonly RpgTextures textures = new();
        private readonly RpgRenderSceneBuilder builder;
        private readonly TileFrame frame = new() { Ambient = Colors.White };
        private readonly Dictionary<string, Control> blocks = new();
        private readonly Dictionary<string, Control> canvases = new();
        private readonly Dictionary<string, Control> markers = new();
        private readonly Dictionary<string, ITileWorldPresentation> views = new();
        private readonly OptionButton toggle;
        private readonly OptionButton neighbourPicker;
        private readonly OptionButton brushPicker;
        private readonly List<string> surfaces = new();
        private TileRenderScene scene;
        private RpgObject shown;
        private bool anchorShown;
        private bool dragging;
        private float[] anchorCells = { 0.5f, 1f };
        private float[] dragStart;

        /// <summary>Raised when a drag ends with a new anchor: <c>[x, y]</c> cells from the origin.</summary>
        public event Action<float[]> AnchorDragged;

        /// <summary>Raised when the footprint brush presses a cell: <c>x, y</c> cells from the sprite's origin.</summary>
        public event Action<int, int> FootprintClicked;

        /// <summary>Raised when the ground around the object is switched in the dropdown: the surface id.</summary>
        public event Action<string> NeighbourPicked;

        /// <summary>Builds the controls; the views come with <see cref="Show"/>.</summary>
        public RpgObjectPreview()
        {
            Name = "RpgObjectPreview";
            SizeFlagsHorizontal = SizeFlags.ExpandFill;
            builder = new RpgRenderSceneBuilder(textures.Image, textures.Charset);

            toggle = new OptionButton { TooltipText = ToolUi.Tr("tools/rpg_object_editor/projection_tip"), Name = "Projection" };
            toggle.AddItem(ToolUi.Tr("tools/rpg_object_editor/projection_both"), 0);
            toggle.AddItem(ToolUi.Tr("tools/rpg_object_editor/projection_top"), 1);
            toggle.AddItem(ToolUi.Tr("tools/rpg_object_editor/projection_iso"), 2);
            toggle.Select(0);
            toggle.ItemSelected += _ => ApplyToggle();
            ToolUi.Field(this, "tools/rpg_object_editor/projection", toggle, "tools/rpg_object_editor/projection_tip");

            neighbourPicker = new OptionButton { TooltipText = ToolUi.Tr("tools/rpg_object_editor/neighbour_tip"), Name = "PreviewNeighbour", SizeFlagsHorizontal = SizeFlags.ExpandFill };
            neighbourPicker.ItemSelected += i => PickNeighbour((int)i);
            ToolUi.Field(this, "tools/rpg_object_editor/neighbour", neighbourPicker, "tools/rpg_object_editor/neighbour_tip");

            brushPicker = new OptionButton { TooltipText = ToolUi.Tr("tools/rpg_object_editor/brush_tip"), Name = "PreviewBrush", SizeFlagsHorizontal = SizeFlags.ExpandFill };
            brushPicker.AddItem(ToolUi.Tr("tools/rpg_object_editor/brush_anchor"), 0);
            brushPicker.AddItem(ToolUi.Tr("tools/rpg_object_editor/brush_footprint"), 1);
            brushPicker.Select(0);
            brushPicker.ItemSelected += _ => RedrawMarkers();
            ToolUi.Field(this, "tools/rpg_object_editor/brush", brushPicker, "tools/rpg_object_editor/brush_tip");

            var row = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
            AddChild(row);
            foreach (var id in new[] { TopDown, Isometric })
            {
                var block = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
                block.AddChild(ToolUi.Hint(ToolUi.Tr(id == TopDown ? "tools/rpg_object_editor/projection_top" : "tools/rpg_object_editor/projection_iso")));
                var canvas = new Control
                {
                    CustomMinimumSize = new Vector2(0, CanvasHeight),
                    SizeFlagsHorizontal = SizeFlags.ExpandFill,
                    ClipContents = true,
                    MouseFilter = MouseFilterEnum.Ignore,
                    Name = "Canvas_" + id
                };
                var projection = id;
                canvas.Resized += () => Fit(projection);
                canvas.GuiInput += ev => OnCanvasInput(projection, ev);
                block.AddChild(canvas);
                row.AddChild(block);
                blocks[id] = block;
                canvases[id] = canvas;
            }
            AddChild(ToolUi.Hint(ToolUi.Tr("tools/rpg_object_editor/drag_hint")));
        }

        /// <summary>The projection toggle's value: <see cref="Both"/>, <c>topdown</c> or <c>isometric</c>.</summary>
        public string Projection => toggle.Selected switch
        {
            1 => TopDown,
            2 => Isometric,
            _ => Both,
        };

        /// <summary>The surface material under and around the object (null when the list has none).</summary>
        public string Neighbour { get; private set; }

        /// <summary>The brush: <see cref="AnchorBrush"/> or <see cref="FootprintBrush"/>.</summary>
        public string Brush => brushPicker.Selected == 1 ? FootprintBrush : AnchorBrush;

        /// <summary>The surfaces the dropdown offers, in its order.</summary>
        public IReadOnlyList<string> Surfaces => surfaces;

        /// <summary>Player figures drawn next to the sprite on the last <see cref="Show"/> (0 for other forms or without a player look).</summary>
        public int FigureCount { get; private set; }

        /// <summary>The projections currently shown, by renderer id.</summary>
        public IReadOnlyList<string> ShownProjections
            => new[] { TopDown, Isometric }.Where(id => blocks[id].Visible).ToList();

        /// <summary>Visuals of the sample scene in that projection, or -1 when the projection has no view (its renderer is not loaded).</summary>
        public int VisualCount(string projection)
            => views.ContainsKey(projection) && scene != null ? scene.Visuals.Count : -1;

        /// <summary>The anchor the handle shows, in cells from the object's origin (null when the handle is hidden: not a sprite).</summary>
        public float[] Anchor => anchorShown ? (float[])anchorCells.Clone() : null;

        /// <summary>Sets the toggle: <see cref="Both"/>, <c>topdown</c> or <c>isometric</c>.</summary>
        public void SelectProjection(string projection)
        {
            toggle.Select(projection == TopDown ? 1 : projection == Isometric ? 2 : 0);
            ApplyToggle();
        }

        /// <summary>Picks the surface of the ground around the object (a value of <see cref="Surfaces"/>); reports it like a click does.</summary>
        public bool SelectNeighbour(string surfaceId)
        {
            var index = surfaces.IndexOf(surfaceId);
            if (index < 0)
                return false;
            neighbourPicker.Select(index);
            PickNeighbour(index);
            return true;
        }

        /// <summary>Sets the brush: <see cref="AnchorBrush"/> or <see cref="FootprintBrush"/>.</summary>
        public void SelectBrush(string brush)
        {
            brushPicker.Select(brush == FootprintBrush ? 1 : 0);
            RedrawMarkers();
        }

        /// <summary>
        /// Draws <paramref name="sample"/> on its sample map in the shown projections; null clears the preview.
        /// <paramref name="surfaceChoices"/> lists the materials the dropdown offers, <paramref name="objects"/> resolves
        /// the neighbour's id, and <paramref name="playerCharset"/> is the walking sheet of the player figures (null: none).
        /// </summary>
        public void Show(RpgObject sample, IEnumerable<string> surfaceChoices, Func<string, RpgObject> objects, string playerCharset)
        {
            Clear();
            UpdateSurfaces(surfaceChoices ?? Array.Empty<string>());
            shown = sample;
            if (sample == null)
                return;
            var neighbour = Neighbour;
            var map = RpgObjectSample.Map(sample, neighbour);
            scene = builder.Build(map, RpgObjectSample.Cells(sample, neighbour, objects), RpgObjectSample.Statics(sample));
            frame.Focus = new Vector2(map.Width, map.Height) / 2f;
            frame.Actors.Clear();
            anchorShown = sample.Form == RpgObjectForm.Sprite;
            anchorCells = sample.Anchor is { Length: 2 } a ? new[] { a[0], a[1] } : new[] { sample.Width / 2f, (float)sample.Height };
            // Only a sprite takes pointer input (the handle and the footprint brush); elsewhere the canvases let the pointer through.
            foreach (var canvas in canvases.Values)
                canvas.MouseFilter = anchorShown ? MouseFilterEnum.Stop : MouseFilterEnum.Ignore;
            brushPicker.Disabled = !anchorShown;
            if (sample.Form == RpgObjectForm.Actor)
            {
                var look = builder.ActorLook(sample, scene.TileSize);
                if (look != null)
                {
                    frame.Actors.Add(new TileActorPose
                    {
                        Id = "sample",
                        Visible = true,
                        Charset = true,
                        Facing = 1,
                        Column = TileWalkSheet.IdleColumn,
                        Position = new Vector2(RpgObjectSample.ActorX, RpgObjectSample.ActorY),
                        Look = look,
                        Tint = Colors.White
                    });
                }
            }
            FigureCount = 0;
            if (anchorShown && !string.IsNullOrEmpty(playerCharset))
            {
                AddFigure("player_behind", RpgObjectSample.BehindCell, playerCharset);
                AddFigure("player_front", RpgObjectSample.InFrontCell, playerCharset);
            }
            foreach (var id in new[] { TopDown, Isometric })
                Mount(id);
            ApplyToggle();
        }

        /// <summary>
        /// Moves the anchor to the cell <paramref name="cell"/> (cell coordinates of the sample map) the way a drag
        /// does on <paramref name="projection"/>: the pointer goes to that cell's centre, the drag ends there.
        /// The tests use it; a real pointer goes through the same handler.
        /// </summary>
        public bool DragAnchorToCell(string projection, int cellX, int cellY)
        {
            if (!views.TryGetValue(projection, out var view) || !anchorShown || Brush != AnchorBrush)
                return false;
            var canvas = canvases[projection];
            var point = PointOf(view, new Vector3(cellX + 0.5f, cellY + 0.5f, 0));
            OnCanvasInput(projection, new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = true, Position = point });
            OnCanvasInput(projection, new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = false, Position = point });
            return canvas != null;
        }

        /// <summary>
        /// A press with the footprint brush on the cell (<paramref name="cellX"/>, <paramref name="cellY"/>) of the sample map,
        /// the way a real press goes on <paramref name="projection"/>. The tests and the documentation use it.
        /// </summary>
        public bool ClickFootprintCell(string projection, int cellX, int cellY)
        {
            if (!views.TryGetValue(projection, out var view) || !anchorShown || Brush != FootprintBrush)
                return false;
            var point = PointOf(view, new Vector3(cellX + 0.5f, cellY + 0.5f, 0));
            OnCanvasInput(projection, new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = true, Position = point });
            return true;
        }

        /// <summary>Releases the views and the scene's textures.</summary>
        public void Clear()
        {
            foreach (var view in views.Values)
                view.Dispose();
            views.Clear();
            markers.Clear();
            scene?.Release();
            scene = null;
            frame.Actors.Clear();
            FigureCount = 0;
        }

        /// <inheritdoc />
        public override void _ExitTree()
        {
            Clear();
            builder.Dispose();
            textures.Dispose();
        }

        // ── context: surfaces and figures ──────────────────────────

        private void UpdateSurfaces(IEnumerable<string> choices)
        {
            var list = choices.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(c => c, StringComparer.Ordinal).ToList();
            if (list.SequenceEqual(surfaces))
                return;
            surfaces.Clear();
            surfaces.AddRange(list);
            neighbourPicker.Clear();
            for (var i = 0; i < surfaces.Count; i++)
                neighbourPicker.AddItem(surfaces[i], i);
            var keep = Neighbour != null ? surfaces.IndexOf(Neighbour) : -1;
            if (keep < 0)
                keep = surfaces.IndexOf(DefaultNeighbour);
            if (keep < 0 && surfaces.Count > 0)
                keep = 0;
            Neighbour = keep >= 0 ? surfaces[keep] : null;
            if (keep >= 0)
                neighbourPicker.Select(keep);
        }

        private void PickNeighbour(int index)
        {
            if (index < 0 || index >= surfaces.Count)
                return;
            Neighbour = surfaces[index];
            NeighbourPicked?.Invoke(Neighbour);
        }

        private void AddFigure(string id, (int X, int Y) cell, string charset)
        {
            var look = builder.ActorLook(new RpgObject { ID = "rpg_object_editor_player", Form = RpgObjectForm.Actor, Charset = charset, Column = TileWalkSheet.IdleColumn }, scene.TileSize);
            if (look == null)
                return;
            frame.Actors.Add(new TileActorPose
            {
                Id = id,
                Visible = true,
                Charset = true,
                Facing = 1,
                Column = TileWalkSheet.IdleColumn,
                Position = new Vector2(cell.X + 0.5f, cell.Y + 1f),
                Look = look,
                Tint = Colors.White,
            });
            FigureCount++;
        }

        // ── views ──────────────────────────────────────────────────

        private void Mount(string projection)
        {
            var canvas = canvases[projection];
            if (!TileRendererRegistry.Instance.Ids.Contains(projection))
            {
                canvas.AddChild(new Label
                {
                    Text = string.Format(ToolUi.Tr("tools/rpg_object_editor/renderer_missing"), projection),
                    AutowrapMode = TextServer.AutowrapMode.WordSmart
                });
                return;
            }
            var view = TileRendererRegistry.Instance.Resolve(projection).Create();
            canvas.AddChild(view.Root);
            view.Mount(scene);
            views[projection] = view;
            AddMarker(projection, canvas);
            Fit(projection);
        }

        /// <summary>The handle and the footprint overlay, drawn above the view of its projection.</summary>
        private void AddMarker(string projection, Control canvas)
        {
            var marker = new Control { MouseFilter = MouseFilterEnum.Ignore, Name = "Anchor_" + projection };
            marker.SetAnchorsPreset(LayoutPreset.FullRect);
            marker.Draw += () => DrawOverlay(projection, marker);
            canvas.AddChild(marker);
            markers[projection] = marker;
            marker.QueueRedraw();
        }

        private void RedrawMarkers()
        {
            foreach (var marker in markers.Values)
                marker.QueueRedraw();
        }

        private void DrawOverlay(string projection, Control marker)
        {
            if (!anchorShown || !views.TryGetValue(projection, out var view))
                return;
            DrawFootprint(view, marker);
            var centre = PointOf(view, new Vector3(RpgObjectSample.SpriteX + anchorCells[0], RpgObjectSample.SpriteY + anchorCells[1], 0));
            marker.DrawCircle(centre, 6f, new Color(1f, 0.85f, 0.2f, 0.9f));
            marker.DrawArc(centre, 9f, 0, Mathf.Tau, 24, new Color(0.1f, 0.1f, 0.1f, 0.9f), 2f);
        }

        /// <summary>The footprint cells of the sprite: blocking cells red, free cells green, each outlined.</summary>
        private void DrawFootprint(ITileWorldPresentation view, Control marker)
        {
            if (shown == null)
                return;
            var blocking = new Color(0.9f, 0.2f, 0.2f, 0.35f);
            var free = new Color(0.3f, 0.9f, 0.4f, 0.25f);
            for (var y = 0; y < shown.Height; y++)
            {
                for (var x = 0; x < shown.Width; x++)
                {
                    var corners = new[] { (0f, 0f), (1f, 0f), (1f, 1f), (0f, 1f) }
                        .Select(c => PointOf(view, new Vector3(RpgObjectSample.SpriteX + x + c.Item1, RpgObjectSample.SpriteY + y + c.Item2, 0)))
                        .ToArray();
                    marker.DrawColoredPolygon(corners, IsBlockingCell(x, y) ? blocking : free);
                    marker.DrawPolyline(new[] { corners[0], corners[1], corners[2], corners[3], corners[0] }, new Color(0.1f, 0.1f, 0.1f, 0.7f), 1f);
                }
            }
        }

        private bool IsBlockingCell(int x, int y)
        {
            var rows = shown.Footprint?.Solid;
            if (rows == null || y >= rows.Count || rows[y] == null)
                return shown.IsSolid;
            return x < rows[y].Length && rows[y][x] == '#';
        }

        /// <summary>A world point of the sample scene in canvas coordinates of its view.</summary>
        private static Vector2 PointOf(ITileWorldPresentation view, Vector3 world)
            => view.Root.Position + view.Root.Scale * view.ProjectToView(world);

        private static Vector2 LocalOf(ITileWorldPresentation view, Vector2 canvasPoint)
            => (canvasPoint - view.Root.Position) / Mathf.Max(0.001f, view.Root.Scale.X);

        private void OnCanvasInput(string projection, InputEvent ev)
        {
            if (!anchorShown || !views.TryGetValue(projection, out var view))
                return;
            if (Brush == FootprintBrush)
            {
                if (ev is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true } press)
                {
                    if (view.TryPickGround(LocalOf(view, press.Position), out var cell))
                        FootprintClicked?.Invoke(cell.X - RpgObjectSample.SpriteX, cell.Y - RpgObjectSample.SpriteY);
                    canvases[projection].AcceptEvent();
                }
                return;
            }
            if (ev is InputEventMouseButton { ButtonIndex: MouseButton.Left } button)
            {
                if (button.Pressed)
                {
                    dragging = true;
                    dragStart = (float[])anchorCells.Clone();
                    MoveAnchorTo(view, button.Position);
                }
                else if (dragging)
                {
                    dragging = false;
                    MoveAnchorTo(view, button.Position);
                    if (dragStart == null || dragStart[0] != anchorCells[0] || dragStart[1] != anchorCells[1])
                        AnchorDragged?.Invoke((float[])anchorCells.Clone());
                }
                canvases[projection].AcceptEvent();
            }
            else if (ev is InputEventMouseMotion motion && dragging)
            {
                MoveAnchorTo(view, motion.Position);
                canvases[projection].AcceptEvent();
            }
        }

        /// <summary>Puts the anchor on the cell under a canvas point (nothing when the point is off the map).</summary>
        private void MoveAnchorTo(ITileWorldPresentation view, Vector2 canvasPoint)
        {
            if (!view.TryPickGround(LocalOf(view, canvasPoint), out var cell))
                return;
            anchorCells = new[] { cell.X - RpgObjectSample.SpriteX + 0.5f, cell.Y - RpgObjectSample.SpriteY + 0.5f };
            RedrawMarkers();
        }

        /// <summary>Scales the scene to its canvas and presents the frame; waits until the canvas has a size.</summary>
        private void Fit(string projection)
        {
            if (!views.TryGetValue(projection, out var view))
                return;
            var size = canvases[projection].Size;
            if (size.X <= 0 || size.Y <= 0)
                return;
            var bounds = view.ContentBounds.Size;
            var scale = Mathf.Min((size.X - 16) / Mathf.Max(1, bounds.X), (size.Y - 16) / Mathf.Max(1, bounds.Y));
            view.Root.Scale = Vector2.One * Mathf.Clamp(scale, 0.05f, 4f);
            view.Resize(size);
            view.Present(frame, 0);
            if (markers.TryGetValue(projection, out var marker))
            {
                canvases[projection].MoveChild(marker, -1);
                marker.QueueRedraw();
            }
        }

        private void ApplyToggle()
        {
            blocks[TopDown].Visible = Projection != Isometric;
            blocks[Isometric].Visible = Projection != TopDown;
            foreach (var id in new[] { TopDown, Isometric })
                Fit(id);
        }
    }
}
