namespace Cthangover.Tools.RpgObjectEditor
{
    using System;
    using System.Collections.Generic;
    using Cthangover.Mods.TileRender;
    using Godot;

    /// <summary>Preview-only scale: the visible body is 1.8 cells tall, independent of source sheet pixels and padding.</summary>
    public static class RpgPreviewFigure
    {
        public const float HeightInCells = 1.8f;
        public static Rect2 Bounds(Vector2 frame, Rect2I body, Vector2 anchor)
        {
            var scale = HeightInCells / Math.Max(1, body.Size.Y);
            return new Rect2(anchor - new Vector2(body.Position.X + body.Size.X / 2f, body.End.Y) * scale, frame * scale);
        }
        public static void Normalize(TileVisual look)
        {
            using var image = look.Texture.GetImage();
            var frame = TileWalkSheet.Frame(look.Texture, TileWalkSheet.Animation(look.Texture).IdleColumn, 0);
            var region = new Rect2I((Vector2I)frame.Position, (Vector2I)frame.Size);
            var body = LargestBody(image, region, 1);
            body.Position -= region.Position;
            look.Bounds = Bounds(frame.Size, body, look.Anchor);
        }
        /// <summary>Largest connected alpha region ignores detached shadows and stray pixels; coarse sampling bounds large room PNGs.</summary>
        public static Rect2I LargestBody(Image image, Rect2I region, int stride)
        {
            var width = (region.Size.X + stride - 1) / stride;
            var height = (region.Size.Y + stride - 1) / stride;
            var mask = new bool[width * height];
            for (var y = 0; y < height; y++) for (var x = 0; x < width; x++)
                mask[y * width + x] = image.GetPixel(Math.Min(region.End.X - 1, region.Position.X + x * stride),
                    Math.Min(region.End.Y - 1, region.Position.Y + y * stride)).A > 0.2f;
            var queue = new Queue<int>(); var best = 0; var bounds = region;
            for (var start = 0; start < mask.Length; start++)
            {
                if (!mask[start]) continue;
                queue.Enqueue(start); mask[start] = false;
                var count = 0; var minX = width; var minY = height; var maxX = 0; var maxY = 0;
                while (queue.Count > 0)
                {
                    var at = queue.Dequeue(); var x = at % width; var y = at / width; count++;
                    minX = Math.Min(minX, x); minY = Math.Min(minY, y); maxX = Math.Max(maxX, x); maxY = Math.Max(maxY, y);
                    if (x > 0) Visit(at - 1, mask, queue);
                    if (x + 1 < width) Visit(at + 1, mask, queue);
                    if (y > 0) Visit(at - width, mask, queue);
                    if (y + 1 < height) Visit(at + width, mask, queue);
                }
                if (count <= best) continue;
                best = count;
                bounds = new Rect2I(region.Position.X + minX * stride, region.Position.Y + minY * stride,
                    Math.Min(region.Size.X - minX * stride, (maxX - minX + 1) * stride), Math.Min(region.Size.Y - minY * stride, (maxY - minY + 1) * stride));
            }
            return bounds;
        }
        private static void Visit(int at, bool[] mask, Queue<int> queue)
        { if (!mask[at]) return; mask[at] = false; queue.Enqueue(at); }
    }
}
