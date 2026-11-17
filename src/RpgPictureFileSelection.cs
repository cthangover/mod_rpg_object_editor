namespace Cthangover.Tools.RpgObjectEditor
{
    using System.IO;
    using Godot;

    /// <summary>Retains the selected dialog until its signal queues that dialog for deletion before importing the file.</summary>
    internal sealed class RpgPictureFileSelection
    {
        private readonly RpgObjectEditorWindow owner;
        private readonly FileDialog dialog;
        internal RpgPictureFileSelection(RpgObjectEditorWindow owner, FileDialog dialog)
        {
            this.owner = owner;
            this.dialog = dialog;
        }

        internal void Selected(string path)
        {
            dialog.QueueFree();
            owner.ImportPicture(path, Path.GetFileNameWithoutExtension(path));
        }
    }
}
