namespace Cthangover.Tools.RpgObjectEditor
{
    using System.Text.Json.Nodes;
    using Cthangover.Tools.EntityEditor;
    using Cthangover.Mods.Interface.Tool;
    using Godot;

    /// <summary>The workspace edits a detached copy. Apply becomes one entity undo step, including foreign patches.</summary>
    public partial class RpgObjectEditorWindow
    {
        private Button visualPartsButton;
        private JsonArray visualParts;
        private string visualObjectId;
        private object visualDocument;
        public RpgPartEditorWindow PartsWindow { get; private set; }
        public void OpenVisualParts()
        {
            var record = Document?.FindAny(SelectedId);
            if (record?.Data == null || FormOf(record.Data) != "sprite") return;
            if (GodotObject.IsInstanceValid(PartsWindow)) { PartsWindow.GrabFocus(); return; }
            var sample = RpgObjectSample.Effective(record.Data, ResolveVisualParent, record.IsForeign);
            visualObjectId = record.Id;
            visualDocument = Document;
            PartsWindow = new RpgPartEditorWindow(sample);
            PartsWindow.Applied += OnVisualPartsApplied;
            ToolWindow.ShowWindow(PartsWindow);
        }
        private static JsonObject ResolveVisualParent(string id) => ForeignEntities.Explain("rpg_object", id)?.Value;
        private void OnVisualPartsApplied(JsonArray parts)
        {
            if (!ReferenceEquals(Document, visualDocument)) return;
            var record = Document?.FindAny(visualObjectId);
            if (record == null) return;
            Select(record); visualParts = parts;
            Edit(ApplyVisualParts); visualParts = null;
        }
        private void ApplyVisualParts(JsonObject root)
        {
            var result = (JsonArray)visualParts.DeepClone();
            if (root["Parts"] is JsonArray original)
                for (var i = 0; i < result.Count; i++)
                {
                    var edited = result[i].AsObject();
                    JsonObject source = null;
                    var id = edited["Id"]?.ToString();
                    if (!string.IsNullOrEmpty(id))
                        foreach (var item in original)
                            if (item?["Id"]?.ToString() == id) { source = item.AsObject(); break; }
                    else if (i < original.Count) source = original[i]?.AsObject();
                    if (source != null)
                        foreach (var field in source)
                            if (!edited.ContainsKey(field.Key)) edited[field.Key] = field.Value?.DeepClone();
                }
            root["Parts"] = result;
        }
        private void CloseVisualPartsWindow()
        {
            if (GodotObject.IsInstanceValid(PartsWindow)) PartsWindow.QueueFree();
        }
    }
}
