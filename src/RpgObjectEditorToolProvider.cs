
namespace Cthangover.Tools.RpgObjectEditor
{
    using Cthangover.Core.UI.Tool;
    using Godot;
    /// <summary>Registers the RPG object editor window opened from the in-game toolbar.</summary>
    public class RpgObjectEditorToolProvider : IToolProvider
    {
        /// <summary>Tool id.</summary>
        public string Id => "rpg_object_editor";

        /// <summary>Display name key.</summary>
        public string LocaleKey => "tools/rpg_object_editor/title";

        /// <summary>Creates the window.</summary>
        public Window CreateWindow() => new RpgObjectEditorWindow();
    }
}
