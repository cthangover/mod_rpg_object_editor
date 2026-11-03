namespace Cthangover.Tools.RpgObjectEditor
{
    using Cthangover.Core.Events;
    using Cthangover.Mods.Interface.Base;
    using Cthangover.Mods.Rpg;
    using Cthangover.Tools.EntityEditor;

    /// <summary>Tool-owned save notification bridge keeps the RPG engine independent of tools_base.</summary>
    public partial class RpgObjectSavedBridge : ModNode
    {
        protected override void OnEnterTree() => ModEventBus.Subscribe<EntitiesSavedEvent>(Saved);
        protected override void OnExitTree() => ModEventBus.Unsubscribe<EntitiesSavedEvent>(Saved);
        private void Saved(EntitiesSavedEvent evt)
        {
            if (evt.Kind == "rpg_object") RpgWorldHost.Current?.RequestObjectRefresh();
        }
    }
}
