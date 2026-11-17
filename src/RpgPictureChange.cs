namespace Cthangover.Tools.RpgObjectEditor
{
    using System.Text.Json.Nodes;

    /// <summary>Keeps a successful import's role and resource id together until its existing Edit call commits them.</summary>
    internal sealed class RpgPictureChange
    {
        private readonly RpgPictureRole role;
        private readonly string id;
        internal RpgPictureChange(RpgPictureRole role, string id)
        {
            this.role = role;
            this.id = id;
        }

        internal void Apply(JsonObject data)
        {
            RpgObjectEditorWindow.SetPicture(data, role, id);
        }
    }
}
