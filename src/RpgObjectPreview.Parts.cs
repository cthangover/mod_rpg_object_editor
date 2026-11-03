namespace Cthangover.Tools.RpgObjectEditor
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Text.Json;
    using Cthangover.Core.Utils;
    using Cthangover.Mods.Rpg.Model;
    using Cthangover.Mods.TileRender;
    using Godot;

    /// <summary>Visual transforms use the renderer's projected axes. Upright pictures use screen axes; ground and quad corners use world axes.</summary>
    public partial class RpgObjectPreview
    {
        public bool PartsEditing { get; private set; }
        public int SelectedPart { get; private set; }
        /// <summary>0: move/resize, 1: place sort point, 2: place a player figure to inspect occlusion.</summary>
        public int PartMode { get; set; }
        public IReadOnlyList<RpgObjectPart> EditableParts => shown?.Parts ?? new List<RpgObjectPart>();
        public event Action PartsChanged;
        public event Action ProbeChanged;
        private bool probeDragging;
        private int activeDragMode;
        /// <summary>Ground position of the test character's feet, independent of part data.</summary>
        public Vector2? ProbePosition
        {
            get { foreach (var actor in frame.Actors) if (actor.Id == "part_probe") return actor.Position + actor.Look.Anchor; return null; }
        }
        private readonly Stack<string> partUndo = new();
        private readonly Stack<string> partRedo = new();
        private readonly Dictionary<string, Image> partImages = new();
        private int partTileSize = 128;
        private bool partDragging;
        private bool partPanning;
        private int partCorner = -1;
        private Vector2 partStart;
        private string partBefore;
        private RpgObjectPart partOriginal;
        private Vector2[] originalCorners;
        private float dragUnit;
        private Transform2D dragWorld;

        public void EnablePartEditing()
        {
            PartsEditing = true;
            GetNode<Control>("AnchorHint").Visible = false;
            partTileSize = Cthangover.Mods.Rpg.RpgWorldHost.Current?.Session?.Map.TileSize ?? 128;
            ((Control)neighbourPicker.GetParent()).Visible = false;
            ((Control)brushPicker.GetParent()).Visible = false;
            foreach (var canvas in canvases.Values) canvas.CustomMinimumSize = new Vector2(0, 540);
        }
        public Control PartCanvas(string projection) => canvases[projection];
        public void SelectPart(int index)
        {
            SelectedPart = Math.Clamp(index, 0, Math.Max(0, EditableParts.Count - 1));
            RedrawMarkers(); PartsChanged?.Invoke();
        }
        private string PartsJson() => JsonSerializer.Serialize(shown.Parts, JsonOptionsProvider.ReadOptions);
        private void CommitPart(string before)
        {
            if (before == PartsJson()) return;
            partUndo.Push(before); partRedo.Clear(); PartsChanged?.Invoke();
        }
        public void AddOverlayPart(string image)
        {
            var before = PartsJson();
            var part = EditableParts.Count > SelectedPart ? CopyPart(shown.Parts[SelectedPart]) : new RpgObjectPart();
            part.Id = "part_" + Guid.NewGuid().ToString("N"); part.Image = image; part.Crop = null;
            part.DrawOrder = TileDrawOrder.ByPosition;
            shown.Parts ??= new List<RpgObjectPart>(); shown.Parts.Add(part); SelectedPart = shown.Parts.Count - 1;
            RebuildParts();
            AutoDepthPoint(false);
            CommitPart(before);
        }
        public void RemoveSelectedPart()
        {
            if (SelectedPart >= EditableParts.Count) return;
            var before = PartsJson(); shown.Parts.RemoveAt(SelectedPart);
            SelectedPart = Math.Max(0, SelectedPart - 1); RebuildParts(); CommitPart(before);
        }
        public void SetPartOrder(TileDrawOrder order)
        {
            if (SelectedPart >= EditableParts.Count) return;
            var before = PartsJson(); shown.Parts[SelectedPart].DrawOrder = order; RebuildParts(); CommitPart(before);
        }
        public void UndoParts()
        {
            CancelPartDrag(); if (partUndo.Count == 0) return;
            partRedo.Push(PartsJson()); RestoreParts(partUndo.Pop());
        }
        public void RedoParts()
        {
            CancelPartDrag(); if (partRedo.Count == 0) return;
            partUndo.Push(PartsJson()); RestoreParts(partRedo.Pop());
        }
        private void RestoreParts(string json)
        {
            shown.Parts = JsonSerializer.Deserialize<List<RpgObjectPart>>(json, JsonOptionsProvider.ReadOptions);
            SelectedPart = Math.Clamp(SelectedPart, 0, Math.Max(0, EditableParts.Count - 1));
            RebuildParts(); PartsChanged?.Invoke();
        }
        public void CancelPartDrag()
        {
            probeDragging = false;
            if (!partDragging) return;
            partDragging = false; RestoreParts(partBefore);
        }
        public void FitParts() { Fit(TopDown); Fit(Isometric); }
        private static RpgObjectPart CopyPart(RpgObjectPart part)
            => JsonSerializer.Deserialize<RpgObjectPart>(JsonSerializer.Serialize(part, JsonOptionsProvider.ReadOptions), JsonOptionsProvider.ReadOptions);
        private TileVisual PartVisual(int index)
        {
            if (index < 0 || index >= EditableParts.Count || scene == null) return null;
            var id = shown.ID + "#" + (shown.Parts[index].Id ?? index.ToString(CultureInfo.InvariantCulture));
            foreach (var visual in scene.Visuals) if (visual.Id == id) return visual;
            return null;
        }
        private Transform2D WorldTransform(ITileWorldPresentation view)
        {
            var origin = PointOf(view, Vector3.Zero);
            return new Transform2D(PointOf(view, new Vector3(1, 0, 0)) - origin,
                PointOf(view, new Vector3(0, 1, 0)) - origin, origin);
        }
        /// <summary>The four visible image corners, used for both drawing and pointer hit testing.</summary>
        public Vector2[] PartCorners(string projection, int index)
        {
            var visual = PartVisual(index);
            if (visual == null || !views.TryGetValue(projection, out var view) || (!string.IsNullOrEmpty(visual.Only) && visual.Only != projection)) return null;
            var points = new Vector2[4];
            if (visual.WorldQuad is { Length: 4 })
                for (var i = 0; i < 4; i++) points[i] = PointOf(view, visual.WorldQuad[i]);
            else if (visual.Upright)
            {
                var anchor = PointOf(view, new Vector3(visual.Anchor.X, visual.Anchor.Y, 0));
                var unit = Math.Abs(WorldTransform(view).X.X);
                var rect = new Rect2(anchor + (visual.Bounds.Position - visual.Anchor) * unit, visual.Bounds.Size * unit);
                points = new[] { rect.Position, rect.Position + new Vector2(rect.Size.X, 0), rect.End, rect.Position + new Vector2(0, rect.Size.Y) };
            }
            else
            {
                var rect = visual.Bounds;
                var transform = WorldTransform(view);
                points = new[] { transform * rect.Position, transform * (rect.Position + new Vector2(rect.Size.X, 0)), transform * rect.End,
                    transform * (rect.Position + new Vector2(0, rect.Size.Y)) };
            }
            return points;
        }
        public Vector2 PartWorldPoint(string projection, Vector2 relative)
            => WorldTransform(views[projection]) * (relative + new Vector2(RpgObjectSample.SpriteX, RpgObjectSample.SpriteY));
        private int PickPart(string projection, Vector2 point)
        {
            for (var i = EditableParts.Count - 1; i >= 0; i--)
            {
                var corners = PartCorners(projection, i); if (corners == null) continue;
                var transform = new Transform2D(corners[1] - corners[0], corners[3] - corners[0], corners[0]);
                if (Mathf.IsZeroApprox(transform.Determinant())) continue;
                var uv = transform.AffineInverse() * point;
                if (uv.X < 0 || uv.Y < 0 || uv.X >= 1 || uv.Y >= 1) continue;
                var visual = PartVisual(i);
                if (!partImages.TryGetValue(shown.Parts[i].Image, out var pixels))
                {
                    pixels = visual.Texture.GetImage(); partImages[shown.Parts[i].Image] = pixels;
                }
                var pixel = visual.Source.Position + uv * visual.Source.Size;
                if (pixels != null && pixels.GetPixel(Math.Clamp((int)pixel.X, 0, pixels.GetWidth() - 1), Math.Clamp((int)pixel.Y, 0, pixels.GetHeight() - 1)).A < 0.05f) continue;
                return i;
            }
            return -1;
        }
        private void DrawPartsOverlay(string projection, Control marker)
        {
            if (ProbePosition is { } feet && views.TryGetValue(projection, out var view))
            {
                var position = PointOf(view, new Vector3(feet.X, feet.Y, 0));
                marker.DrawCircle(position, 11, new Color(0.1f, 0.7f, 1f, 0.9f));
                marker.DrawArc(position, 14, 0, Mathf.Tau, 24, Colors.White, 2);
                marker.DrawString(marker.GetThemeDefaultFont(), position + new Vector2(18, -4),
                    Cthangover.Tools.Common.ToolUi.Tr("tools/rpg_object_editor/v2_drag_player"), fontSize: 15, modulate: Colors.White);
            }
            var corners = PartCorners(projection, SelectedPart);
            if (corners == null) return;
            marker.DrawPolyline(new[] { corners[0], corners[1], corners[2], corners[3], corners[0] }, Colors.Gold, 2);
            foreach (var corner in corners) marker.DrawRect(new Rect2(corner - Vector2.One * 5, Vector2.One * 10), Colors.White);
            var sort = DepthPoint(projection);
            if (shown.Parts[SelectedPart].DrawOrder == TileDrawOrder.ByPosition)
            {
                marker.DrawLine(new Vector2(0, sort.Y), new Vector2(canvases[projection].Size.X, sort.Y), new Color(1f, 0.4f, 0.2f, 0.6f), 2);
                marker.DrawString(marker.GetThemeDefaultFont(), new Vector2(8, sort.Y - 8),
                    Cthangover.Tools.Common.ToolUi.Tr("tools/rpg_object_editor/v2_behind_line"), fontSize: 14, modulate: Colors.White);
                marker.DrawString(marker.GetThemeDefaultFont(), new Vector2(8, sort.Y + 22),
                    Cthangover.Tools.Common.ToolUi.Tr("tools/rpg_object_editor/v2_front_line"), fontSize: 14, modulate: Colors.White);
            }
            marker.DrawCircle(sort, 7, Colors.OrangeRed);
            marker.DrawLine(sort - Vector2.Right * 12, sort + Vector2.Right * 12, Colors.White, 2);
            marker.DrawLine(sort - Vector2.Down * 12, sort + Vector2.Down * 12, Colors.White, 2);
        }
        private void HandlePartsInput(string projection, InputEvent ev)
        {
            if (!views.TryGetValue(projection, out var view)) return;
            var canvas = canvases[projection];
            if (ev is InputEventMouseButton wheel && wheel.Pressed && wheel.ButtonIndex is MouseButton.WheelUp or MouseButton.WheelDown)
            {
                var old = view.Root.Scale.X;
                var next = Mathf.Clamp(old * (wheel.ButtonIndex == MouseButton.WheelUp ? 1.15f : 1 / 1.15f), 0.02f, 8);
                view.Root.Position = wheel.Position - (wheel.Position - view.Root.Position) * next / old;
                view.Root.Scale = Vector2.One * next; RedrawMarkers(); canvas.AcceptEvent(); return;
            }
            if (ev is InputEventMouseButton { ButtonIndex: MouseButton.Middle } pan) { partPanning = pan.Pressed; canvas.AcceptEvent(); return; }
            if (ev is InputEventMouseMotion motion)
            {
                if (partPanning) { view.Root.Position += motion.Relative; RedrawMarkers(); }
                else if (probeDragging) PlaceProbe(projection, motion.Position);
                else if (partDragging) MovePart(projection, motion.Position);
                canvas.AcceptEvent(); return;
            }
            if (ev is not InputEventMouseButton { ButtonIndex: MouseButton.Left } button) return;
            canvas.AcceptEvent();
            if (!button.Pressed)
            {
                if (probeDragging) { PlaceProbe(projection, button.Position); probeDragging = false; return; }
                if (!partDragging) return;
                MovePart(projection, button.Position); partDragging = false; CommitPart(partBefore); return;
            }
            if (PartMode == 2 || (ProbePosition is { } feet && PointOf(view, new Vector3(feet.X, feet.Y, 0)).DistanceTo(button.Position) <= 16))
            { probeDragging = true; PlaceProbe(projection, button.Position); return; }
            activeDragMode = PartMode;
            if (SelectedPart < EditableParts.Count && DepthPoint(projection).DistanceTo(button.Position) <= 12) activeDragMode = 1;
            partCorner = -1;
            var selectedCorners = PartCorners(projection, SelectedPart);
            if (activeDragMode == 0 && selectedCorners != null)
                for (var i = 0; i < 4; i++) if (selectedCorners[i].DistanceTo(button.Position) <= 10) { partCorner = i; break; }
            if (activeDragMode == 0 && partCorner < 0)
            {
                var picked = PickPart(projection, button.Position);
                if (picked < 0) return;
                SelectPart(picked);
            }
            if (SelectedPart >= EditableParts.Count) return;
            // Plane pictures are shown faithfully; their geometry stays in the advanced form until a plane-specific manipulator exists.
            if (activeDragMode == 0 && shown.Parts[SelectedPart].Geometry == RpgPartGeometry.Plane) return;
            partBefore = PartsJson(); partOriginal = CopyPart(shown.Parts[SelectedPart]);
            partStart = button.Position; originalCorners = PartCorners(projection, SelectedPart);
            dragWorld = WorldTransform(view); dragUnit = Math.Abs(dragWorld.X.X);
            partDragging = true;
            if (activeDragMode == 1) MovePart(projection, button.Position);
        }
        private void MovePart(string projection, Vector2 point)
        {
            if (!partDragging || originalCorners == null) return;
            var part = shown.Parts[SelectedPart];
            var origin = new Vector2(RpgObjectSample.SpriteX, RpgObjectSample.SpriteY);
            var delta = point - partStart;
            if (activeDragMode == 1)
            {
                var anchor = shown.Anchor is { Length: >= 2 } a ? new Vector2(a[0], a[1]) : new Vector2(shown.Width / 2f, shown.Height);
                var offset = dragWorld.AffineInverse() * point - origin - anchor;
                part.SortOffset = new[] { offset.X, offset.Y }; part.DrawOrder = TileDrawOrder.ByPosition;
            }
            else if (partOriginal.Geometry == RpgPartGeometry.Quad)
            {
                var move = dragWorld.BasisXformInv(delta);
                part.Points = new List<float[]>();
                for (var i = 0; i < 4; i++)
                {
                    var p = partOriginal.Points[i];
                    var d = partCorner < 0 || partCorner == i ? move : Vector2.Zero;
                    part.Points.Add(new[] { p[0] + d.X, p[1] + d.Y, p[2] });
                }
            }
            else
            {
                var upright = partOriginal.Geometry == RpgPartGeometry.Upright;
                var move = upright ? delta / dragUnit : dragWorld.BasisXformInv(delta);
                var visualSize = upright ? (originalCorners[2] - originalCorners[0]) / dragUnit
                    : dragWorld.AffineInverse() * originalCorners[2] - dragWorld.AffineInverse() * originalCorners[0];
                var at = partOriginal.At is { Length: >= 2 } p ? new Vector2(p[0], p[1]) : Vector2.Zero;
                if (partCorner < 0) at += move;
                else
                {
                    var end = at + visualSize;
                    if (partCorner is 0 or 3) at.X = Math.Min(end.X - 0.02f, at.X + move.X); else end.X = Math.Max(at.X + 0.02f, end.X + move.X);
                    if (partCorner is 0 or 1) at.Y = Math.Min(end.Y - 0.02f, at.Y + move.Y); else end.Y = Math.Max(at.Y + 0.02f, end.Y + move.Y);
                    visualSize = end - at;
                    part.Size = new[] { visualSize.X, visualSize.Y };
                }
                part.At = new[] { at.X, at.Y };
            }
            RebuildParts();
        }
        private void RebuildParts()
        {
            var positions = new Dictionary<string, Vector2>(); var scales = new Dictionary<string, Vector2>();
            foreach (var pair in views) { positions[pair.Key] = PointOf(pair.Value, Vector3.Zero); scales[pair.Key] = pair.Value.Root.Scale; pair.Value.Dispose(); }
            views.Clear(); foreach (var marker in markers.Values) marker.Free(); markers.Clear();
            var previous = scene;
            var map = RpgObjectSample.Map(shown); map.TileSize = partTileSize;
            scene = builder.Build(map, RpgObjectSample.Cells(shown), RpgObjectSample.Statics(shown));
            foreach (var id in new[] { TopDown, Isometric })
            {
                Mount(id);
                if (positions.TryGetValue(id, out var position) && views.TryGetValue(id, out var view))
                {
                    view.Root.Scale = scales[id];
                    view.Root.Position += position - PointOf(view, Vector3.Zero);
                }
            }
            previous?.Release(); RedrawMarkers();
        }
        private void PlaceProbe(string projection, Vector2 point)
        {
            var view = views[projection]; var cell = WorldTransform(view).AffineInverse() * point;
            TileActorPose probe = null;
            foreach (var actor in frame.Actors) if (actor.Id == "part_probe") probe = actor;
            if (probe == null && frame.Actors.Count > 0)
            {
                probe = new TileActorPose { Id = "part_probe", Visible = true, Charset = !PartsEditing, Look = frame.Actors[0].Look,
                    Facing = 1, Column = TileWalkSheet.IdleColumn, Tint = Colors.White };
                frame.Actors.Add(probe);
            }
            if (probe == null) return;
            probe.Position = cell - probe.Look.Anchor;
            foreach (var pair in views)
            {
                var position = pair.Value.Root.Position;
                pair.Value.Present(frame, 0);
                pair.Value.Root.Position = position;
            }
            RedrawMarkers(); ProbeChanged?.Invoke();
        }
        private void ClearPartImages() { foreach (var image in partImages.Values) image?.Dispose(); partImages.Clear(); }
    }
}
