namespace Cthangover.Tools.RpgObjectEditor
{
    using Cthangover.Core.Mods;
    public class RpgObjectEditorInitializer : IModInitializer
    {
        private static bool queued;
        public void OnModLoaded(string modId) { }
        public void OnModResourcesReady()
        {
            if (queued) return;
            queued = true; ModRoot.AddDeferred(new RpgObjectSavedBridge());
        }
    }
}
