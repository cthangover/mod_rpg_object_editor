
namespace Cthangover.Tools.RpgObjectEditor
{
    using Cthangover.Core.UI.Tool;
    /// <summary>In-game toolbar button of the RPG object editor.</summary>
    public class RpgObjectEditorToolBoxButton : IToolBoxButton
    {
        /// <summary>Matches <see cref="RpgObjectEditorToolProvider.Id"/>.</summary>
        public string ToolId => "rpg_object_editor";

        /// <summary>No icon: the button shows its label.</summary>
        public string IconPath => "";

        /// <summary>Button label key.</summary>
        public string LocaleKey => "tools/rpg_object_editor/title";

        /// <summary>Always shown.</summary>
        public bool IsVisible() => true;
    }
}
