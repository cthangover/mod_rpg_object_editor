
namespace Cthangover.Tools.RpgObjectEditor.Tests
{
    using System.Linq;
    using Cthangover.Core.Mods;
    using Cthangover.Core.Utils;
    using Godot;
    /// <summary>Adds the editor test driver when <c>--itest=rpg_object_editor_edit</c> is given.</summary>
    public class RpgObjectEditorTestInitializer : IModInitializer
    {
        private static bool driverQueued;

        /// <summary>Nothing per mod.</summary>
        public void OnModLoaded(string modId)
        {
        }

        /// <summary>Queues the driver once.</summary>
        public void OnModResourcesReady()
        {
            var args = OS.GetCmdlineUserArgs();
            if (driverQueued || !args.Any(a => a == "--itest=rpg_object_editor_edit"))
                return;

            driverQueued = true;
            GameLogger.Log("TEST", "RpgObjectEditorTestInitializer: activating rpg_object_editor_edit");
            ModRoot.AddDeferred(new RpgObjectEditorTestDriver());
        }
    }
}
