namespace Cthangover.Tools.RpgObjectEditor.Tests.Visual
{
    using Cthangover.Mods.TileRender;
    using Godot;

    /// <summary>Reads actual renderer pixels at an overlapping part/actor region on opposite sides of a depth point.</summary>
    public partial class RpgDepthPixels : Node
    {
        private SubViewport viewport;
        private ITileWorldPresentation view;
        private TileRenderScene scene;
        private readonly TileFrame frame = new() { Ambient = Colors.White, Focus = new Vector2(3, 3) };
        private TileActorPose actor;
        private ImageTexture desk;
        private ImageTexture hero;
        private readonly Vector2 anchor = new(3, 3);
        public override void _Ready()
        {
            viewport = new SubViewport { Size = new Vector2I(256, 256), TransparentBg = true,
                RenderTargetUpdateMode = SubViewport.UpdateMode.Always, Disable3D = true };
            AddChild(viewport);
            using var image = Image.CreateEmpty(16, 16, false, Image.Format.Rgba8);
            image.Fill(Colors.Lime); desk = ImageTexture.CreateFromImage(image);
            image.Fill(Colors.Red); hero = ImageTexture.CreateFromImage(image);
            scene = new TileRenderScene { Size = new Vector2I(6, 6), TileSize = 32, Zoom = 1 };
            scene.Visuals.Add(new TileVisual { Id = "desk", Texture = desk, Source = new Rect2(0, 0, 16, 16),
                Bounds = new Rect2(anchor - new Vector2(1, 2), new Vector2(2, 2)), Anchor = anchor, Upright = true, DrawOrder = TileDrawOrder.ByPosition });
            actor = new TileActorPose { Id = "hero", Visible = true, Tint = Colors.White, Look = new TileVisual
            { Texture = hero, Source = new Rect2(0, 0, 16, 16), Bounds = new Rect2(-0.5f, -1.5f, 1, 1.5f), Anchor = Vector2.Zero, Upright = true } };
            frame.Actors.Add(actor);
            view = TileRendererRegistry.Instance.Resolve("isometric").Create(); viewport.AddChild(view.Root);
            view.Mount(scene); view.Resize(new Vector2(256, 256)); SetFront(false);
        }
        public void SetFront(bool front)
        {
            actor.Position = anchor + Vector2.One * (front ? 0.25f : -0.25f); view.Present(frame, 0);
        }
        public Color Pixel()
        {
            var point = view.ProjectToView(new Vector3(anchor.X, anchor.Y, 0)) - new Vector2(0, 24);
            using var image = viewport.GetTexture().GetImage(); return image.GetPixel((int)point.X, (int)point.Y);
        }
        public override void _ExitTree()
        {
            view?.Dispose(); scene?.Release(); desk?.Dispose(); hero?.Dispose();
        }
    }
}
