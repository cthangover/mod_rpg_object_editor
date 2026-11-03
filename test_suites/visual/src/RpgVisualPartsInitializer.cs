namespace Cthangover.Tools.RpgObjectEditor.Tests.Visual
{
    using Cthangover.Core.Mods;
    using Godot;
    public class RpgVisualPartsInitializer : IModInitializer
    {
        private static bool queued;
        public void OnModLoaded(string modId) { }
        public void OnModResourcesReady()
        {
            if (queued) return;
            foreach (var arg in OS.GetCmdlineUserArgs()) if (arg == "--itest=rpg_visual_parts")
            { queued = true; ModRoot.AddDeferred(new RpgVisualPartsDriver()); return; }
        }
    }
}
