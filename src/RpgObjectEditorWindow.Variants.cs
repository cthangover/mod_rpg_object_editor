namespace Cthangover.Tools.RpgObjectEditor
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.Json.Nodes;
    using Cthangover.Mods.Interface.Tool;
    using Cthangover.Mods.Rpg;
    using Cthangover.Mods.Rpg.Model;
    using Cthangover.Tools.Common;
    using Cthangover.Tools.EntityEditor;
    using Cthangover.Tools.Services;
    using Godot;

    /// <summary>
    /// Inheritance and variants of the object editor: "Make a variant" (a new own object with <c>Extends</c> set to the
    /// selected one), the parent's picture next to the <c>Extends</c> field, the parts a sprite inherits (read-only
    /// until "Override" copies one), and the footprint brush's edits of the own footprint.
    /// </summary>
    public partial class RpgObjectEditorWindow
    {
        /// <summary>The largest footprint side the editor writes (the spin boxes stop there too).</summary>
        private const int MaxFootprintSide = 12;

        private TextureRect parentThumb;
        private Label parentLabel;

        /// <summary>Adds the parent's picture and id to the <c>Extends</c> row the base window builds.</summary>
        private void AttachParentRow()
        {
            var label = FindChildren("*", "Label", true, false).OfType<Label>().FirstOrDefault(l => l.Text == ToolUi.Tr("tools/rpg_object_editor/extends"));
            if (label?.GetParent() is not HBoxContainer row)
                return;
            parentThumb = new TextureRect
            {
                Name = "ExtendsThumb",
                CustomMinimumSize = new Vector2(48, 48),
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                Visible = false,
            };
            parentLabel = new Label
            {
                Name = "ExtendsParent",
                CustomMinimumSize = new Vector2(180, 0),
                ClipText = true,
                Visible = false,
                TooltipText = ToolUi.Tr("tools/rpg_object_editor/extends_parent_tip"),
            };
            row.AddChild(parentThumb);
            row.AddChild(parentLabel);
        }

        /// <summary>The parent's picture and id next to the field; hidden when the object has no parent.</summary>
        private void UpdateParentRow(EntityRecord record)
        {
            if (parentThumb == null || parentLabel == null)
                return;
            var parentId = record?.Data == null ? null : Text(record.Data, "Extends");
            var parent = string.IsNullOrEmpty(parentId) ? null : ForeignEntities.Explain("rpg_object", parentId)?.Value;
            parentLabel.Visible = !string.IsNullOrEmpty(parentId);
            parentLabel.Text = parent == null && !string.IsNullOrEmpty(parentId)
                ? string.Format(ToolUi.Tr("tools/rpg_object_editor/parent_missing"), parentId)
                : parentId ?? string.Empty;
            parentThumb.Texture = null;
            parentThumb.Visible = parent != null;
            if (parent == null)
                return;
            var (group, path) = ThumbnailOf(parent);
            parentThumb.Texture = group == null || path == null ? null : ModResourceService.LoadGroupImage(group, path);
        }

        /// <summary>Asks for the name of a new object that inherits the selected one.</summary>
        private void AskVariantName()
        {
            if (Document?.FindAny(SelectedId)?.Data == null)
                return;
            var dialog = new AcceptDialog
            {
                Name = "MakeVariantDialog",
                Title = ToolUi.Tr("tools/rpg_object_editor/make_variant_title"),
                DialogText = ToolUi.Tr("tools/rpg_object_editor/make_variant_hint"),
                OkButtonText = ToolUi.Tr("tools/rpg_object_editor/make_variant_ok"),
                MinSize = new Vector2I(460, 200),
                Theme = ToolTheme.Get(),
            };
            // The dialog's text label is at the top; the field sits under it.
            var edit = new LineEdit
            {
                Name = "MakeVariantName",
                PlaceholderText = ToolUi.Tr("tools/rpg_object_editor/make_variant_name"),
                Position = new Vector2(16, 72),
                Size = new Vector2(428, 32),
            };
            dialog.AddChild(edit);
            void Create()
            {
                var typed = edit.Text;
                dialog.Hide();
                dialog.QueueFree();
                MakeVariant(typed);
            }
            edit.TextSubmitted += _ => Create();
            dialog.Confirmed += Create;
            dialog.CloseRequested += dialog.QueueFree;
            dialog.Canceled += dialog.QueueFree;
            AddChild(dialog);
            dialog.PopupCentered();
            edit.GrabFocus();
        }

        /// <summary>
        /// Makes a new own object of the selected one (own or foreign): <c>Extends</c> is the selected object, the form is its
        /// form, the name is <paramref name="name"/> in every language of the mod. The new object is listed and opened in the
        /// form; Save writes it into the mod's <c>objects</c> file. Returns its id, or null when nothing is selected or the name is empty.
        /// </summary>
        public string MakeVariant(string name)
        {
            var parent = Document?.FindAny(SelectedId);
            var typed = (name ?? string.Empty).Trim();
            if (parent?.Data == null || typed.Length == 0)
            {
                SetStatus(ToolUi.Tr("tools/rpg_object_editor/make_variant_empty"), ToolStatusKind.Error);
                return null;
            }
            var id = Document.FreeId(RpgObjectVariant.IdFor(ModId, typed, parent.Id));
            var key = id + "/name";
            var form = FormOf(parent.Data);
            var entity = new JsonObject
            {
                ["Id"] = id,
                ["Form"] = form.Length > 0 ? form : "sprite",
                ["Extends"] = parent.Id,
                ["Name"] = key,
            };
            Document.BeginChange();
            foreach (var language in LocaleFileService.Languages(ModId))
                Document.SetText(language, key, typed);
            var record = Document.Add(entity);
            SetDirty();
            RefreshList();
            Select(record);
            SetStatus(string.Format(ToolUi.Tr("tools/rpg_object_editor/variant_created"), id, parent.Id), ToolStatusKind.Ok);
            return id;
        }

        /// <summary>Picks the surface material under and around the object in the preview (a dropdown value; nothing is saved).</summary>
        public bool PickGround(string surfaceId) => preview != null && preview.SelectNeighbour(surfaceId);

        /// <summary>Sets the preview's brush: <c>anchor</c> (drag the handle) or <c>footprint</c> (click cells).</summary>
        public void PickBrush(string brush) => preview?.SelectBrush(brush);

        /// <summary>Selects an object of the edited mod, or a foreign one, by id.</summary>
        public bool ShowObject(string id)
        {
            var record = Document?.FindAny(id);
            if (record == null)
            {
                SetStatus(string.Format(ToolUi.Tr("tools/rpg_object_editor/object_missing"), id), ToolStatusKind.Error);
                return false;
            }
            Select(record);
            return true;
        }

        /// <summary>
        /// The parts a sprite inherits from its <c>Extends</c> parent, as read-only rows that name the parent. They show only while
        /// the object has no <c>Parts</c> of its own: the merger replaces an array that has no operation, so an own list replaces the
        /// parent's whole list. "Override" therefore copies the parent's whole list into the object; every part then becomes editable
        /// here, and the one clicked is the one the author came for.
        /// </summary>
        private void AddInheritedParts(JsonObject root)
        {
            if (root["Parts"] != null)
                return;
            var parentId = Text(root, "Extends");
            if (string.IsNullOrEmpty(parentId))
                return;
            var parent = ForeignEntities.Explain("rpg_object", parentId)?.Value;
            if (parent == null)
            {
                blocksHost.AddChild(ToolUi.Hint(string.Format(ToolUi.Tr("tools/rpg_object_editor/parent_missing"), parentId)));
                return;
            }
            if (parent["Parts"] is not JsonArray inherited)
                return;
            for (var i = 0; i < inherited.Count; i++)
            {
                if (inherited[i] is not JsonObject part)
                    continue;
                var id = Text(part, "Id") ?? string.Empty;
                var index = i;
                var line = new Label
                {
                    Name = "Inherited_" + index,
                    Text = string.Format(ToolUi.Tr("tools/rpg_object_editor/inherited_part"), id.Length > 0 ? id : "-", Text(part, "Image") ?? "-", parentId),
                    AutowrapMode = TextServer.AutowrapMode.Arbitrary,
                    SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                    TooltipText = ToolUi.Tr("tools/rpg_object_editor/inherited_tip"),
                    Modulate = new Color(1f, 1f, 1f, 0.55f),
                };
                AddRow(line, ButtonOf("tools/rpg_object_editor/inherited_override", () => Edit(r =>
                {
                    var list = Arr(r, "Parts");
                    foreach (var inheritedPart in inherited)
                        list.Add(inheritedPart?.DeepClone());
                }), "InheritedOverride_" + index));
            }
        }

        /// <summary>A sprite's footprint becomes its own when it is first edited: an inherited footprint is copied into the object.</summary>
        private static void EnsureOwnFootprint(JsonObject root)
        {
            if (root["Footprint"] != null)
                return;
            var parentId = Text(root, "Extends");
            var parent = string.IsNullOrEmpty(parentId) ? null : ForeignEntities.Explain("rpg_object", parentId)?.Value;
            if (parent?["Footprint"] is JsonObject inherited)
                root["Footprint"] = inherited.DeepClone();
        }

        /// <summary>
        /// A press on the footprint of the preview. Inside the footprint the cell flips between blocking (#) and free (.).
        /// Right of or below it the footprint grows to that cell, and the new cell blocks. The footprint grows from the sprite's
        /// origin cell, so the cells left of or above it are refused.
        /// </summary>
        private void OnFootprintClicked(int x, int y)
        {
            if (x < 0 || y < 0 || x >= MaxFootprintSide || y >= MaxFootprintSide)
            {
                SetStatus(ToolUi.Tr("tools/rpg_object_editor/footprint_out"), ToolStatusKind.Error);
                return;
            }
            Edit(root =>
            {
                EnsureOwnFootprint(root);
                PaintFootprint(root, x, y);
            });
        }

        private static void PaintFootprint(JsonObject root, int x, int y)
        {
            var (width, height) = FootprintOf(root);
            var mask = MaskOf(root, width, height);
            var newWidth = Math.Max(width, x + 1);
            var newHeight = Math.Max(height, y + 1);
            var grid = new bool[newWidth, newHeight];
            for (var row = 0; row < height; row++)
            {
                for (var col = 0; col < width; col++)
                    grid[col, row] = mask[col, row];
            }
            if (x >= width || y >= height)
                grid[x, y] = true;
            else
                grid[x, y] = !grid[x, y];

            var rows = new JsonArray();
            for (var row = 0; row < newHeight; row++)
                rows.Add(JsonValue.Create(new string(Enumerable.Range(0, newWidth).Select(col => grid[col, row] ? '#' : '.').ToArray())));
            var footprint = Obj(root, "Footprint");
            footprint["Size"] = RpgObjectNumbers.ToIntNode(new[] { newWidth, newHeight });
            footprint["Solid"] = rows;
        }
    }
}
