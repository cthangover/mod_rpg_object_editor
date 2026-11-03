namespace Cthangover.Tools.RpgObjectEditor
{
    using System;
    using Cthangover.Mods.Rpg.Model;
    using Cthangover.Mods.TileRender;
    using Godot;

    /// <summary>Explicit depth tools share the renderer's actual foot points; the guide line and actor dragging use those same coordinates.</summary>
    public partial class RpgObjectPreview
    {
        public float ProbeHeightInCells => RpgPreviewFigure.HeightInCells;
        public Vector2 DepthPoint(string projection)
        {
            var anchor = shown.Anchor is { Length: >= 2 } a ? new Vector2(a[0], a[1]) : new Vector2(shown.Width / 2f, shown.Height);
            if (SelectedPart < EditableParts.Count && shown.Parts[SelectedPart].SortOffset is { Length: >= 2 } offset)
                anchor += new Vector2(offset[0], offset[1]);
            return PartWorldPoint(projection, anchor);
        }
        public bool ProbeIsInFront(string projection)
        {
            if (SelectedPart >= EditableParts.Count || ProbePosition is not { } feet) return false;
            var part = shown.Parts[SelectedPart];
            var order = part.DrawOrder ?? shown.DrawOrder ?? TileDrawOrder.ByPosition;
            if (order != TileDrawOrder.ByPosition) return order == TileDrawOrder.BelowCharacters;
            var screen = PointOf(views[projection], new Vector3(feet.X, feet.Y, 0));
            return screen.Y > DepthPoint(projection).Y;
        }
        public void PlaceProbeSide(bool front)
        {
            var projection = Projection == TopDown ? TopDown : Isometric;
            var unit = Math.Abs(WorldTransform(views[projection]).X.X);
            PlaceProbe(projection, DepthPoint(projection) + new Vector2(0, unit * (front ? 0.4f : -0.4f)));
        }
        public void MovePartOrder(int delta)
        {
            var next = SelectedPart + delta;
            if (next < 0 || next >= EditableParts.Count) return;
            var before = PartsJson();
            (shown.Parts[SelectedPart], shown.Parts[next]) = (shown.Parts[next], shown.Parts[SelectedPart]);
            SelectedPart = next; RebuildParts(); CommitPart(before);
        }
        public void FocusSelectedPart()
        {
            var projection = Projection == TopDown ? TopDown : Isometric;
            var corners = PartCorners(projection, SelectedPart); var visual = PartVisual(SelectedPart);
            if (corners == null || visual?.Texture == null) return;
            using var image = visual.Texture.GetImage();
            var source = new Rect2I((Vector2I)visual.Source.Position, (Vector2I)visual.Source.Size);
            var body = RpgPreviewFigure.LargestBody(image, source, Math.Max(1, source.Size.X / 400));
            var top = ((Vector2)body.Position - visual.Source.Position) / visual.Source.Size;
            var bottom = ((Vector2)body.End - visual.Source.Position) / visual.Source.Size;
            var a = corners[0] + (corners[1] - corners[0]) * top.X + (corners[3] - corners[0]) * top.Y;
            var b = corners[0] + (corners[1] - corners[0]) * bottom.X + (corners[3] - corners[0]) * bottom.Y;
            var size = (b - a).Abs(); var view = views[projection]; var available = canvases[projection].Size;
            var factor = Math.Min(available.X / Math.Max(1, size.X * 3), available.Y / Math.Max(1, size.Y * 3));
            var next = Mathf.Clamp(view.Root.Scale.X * factor, 0.02f, 8);
            view.Root.Position = available / 2 - ((a + b) / 2 - view.Root.Position) * next / view.Root.Scale.X;
            view.Root.Scale = Vector2.One * next; RedrawMarkers();
        }

        public void AutoDepthPoint(bool record = true)
        {
            if (SelectedPart >= EditableParts.Count) return;
            var projection = Projection == TopDown ? TopDown : Isometric;
            var corners = PartCorners(projection, SelectedPart);
            var visual = PartVisual(SelectedPart);
            if (corners == null || visual?.Texture == null) return;
            var before = PartsJson();
            using var pixels = visual.Texture.GetImage();
            var source = new Rect2I((Vector2I)visual.Source.Position, (Vector2I)visual.Source.Size);
            var bounds = RpgPreviewFigure.LargestBody(pixels, source, Math.Max(1, source.Size.X / 400));
            var uv = (new Vector2(bounds.Position.X + bounds.Size.X / 2f, bounds.End.Y) - visual.Source.Position) / visual.Source.Size;
            var screen = corners[0] + (corners[1] - corners[0]) * uv.X + (corners[3] - corners[0]) * uv.Y;
            var anchor = shown.Anchor is { Length: >= 2 } a ? new Vector2(a[0], a[1]) : new Vector2(shown.Width / 2f, shown.Height);
            var offset = WorldTransform(views[projection]).AffineInverse() * screen
                - new Vector2(RpgObjectSample.SpriteX, RpgObjectSample.SpriteY) - anchor;
            shown.Parts[SelectedPart].SortOffset = new[] { offset.X, offset.Y };
            shown.Parts[SelectedPart].DrawOrder = TileDrawOrder.ByPosition;
            RebuildParts();
            if (record) CommitPart(before);
        }
    }
}
