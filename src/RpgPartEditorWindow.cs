using System;
using System.Text.Json.Nodes;
using Cthangover.Mods.Interface.Tool;
using Cthangover.Mods.Rpg.Model;
using Cthangover.Mods.Rpg.Validation;
using Godot;

namespace Cthangover.Tools.RpgObjectEditor
{
    /// <summary>Window lifetime and Apply contract around an owning ModWidget workspace; invalid candidates stay editable.</summary>
    public partial class RpgPartEditorWindow : ToolWindow
    {
        private readonly RpgPartEditorWorkbench workbench;

        /// <summary>Borrowed preview owned by this window's widget; its candidate never changes the parent document directly.</summary>
        public RpgObjectPreview Canvas
        {
            get
            {
                return workbench.Canvas;
            }
        }

        /// <summary>Receives a complete validated candidate once; the parent commits its existing one-step edit transaction.</summary>
        public event Action<JsonArray> Applied;

        /// <summary>Creates one widget owner for the utility layout, detached editing data, bindings and native resource cleanup.</summary>
        public RpgPartEditorWindow(RpgObject sample) : base("tools/rpg_object_editor/visual_parts")
        {
            Name = "VisualPartsWindow";
            workbench = new RpgPartEditorWorkbench(sample);
            AddChild(workbench);
            workbench.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
            workbench.EnsureConstructed();
            workbench.Applied += OnApplied;
            workbench.Rejected += OnRejected;
            workbench.Cancelled += OnCancelled;
        }

        /// <summary>Validates before serialization or publication; refusal retains the window, values and parent undo history.</summary>
        public void Apply()
        {
            workbench.Apply();
        }

        private void OnApplied(JsonArray candidate)
        {
            Applied?.Invoke(candidate);
            QueueFree();
        }

        private void OnRejected(RpgDataIssue issue)
        {
            SetStatus(issue.Field + ": " + RpgDataMessages.Explain(issue.Code), ToolStatusKind.Error);
        }

        private void OnCancelled()
        {
            QueueFree();
        }

        protected override bool HandleKey(InputEventKey key)
        {
            return workbench.HandleEditorKey(key);
        }

        protected override void Cleanup()
        {
            workbench.Applied -= OnApplied;
            workbench.Rejected -= OnRejected;
            workbench.Cancelled -= OnCancelled;
            base.Cleanup();
        }
    }
}
