namespace Cthangover.Tools.RpgObjectEditor
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text.Json;
    using System.Text.Json.Nodes;
    using Cthangover.Core.Mods;
    using Cthangover.Core.Mods.Resolvers;
    using Cthangover.Core.UI.Theming;
    using Cthangover.Core.Utils;
    using Cthangover.Mods.Interface.Theming;
    using Cthangover.Mods.Interface.Tool;
    using Cthangover.Mods.Rpg;
    using Cthangover.Mods.Rpg.Model;
    using Cthangover.Tools.Common;
    using Cthangover.Tools.EntityEditor;
    using Cthangover.Tools.Services;
    using Godot;

    /// <summary>
    /// Editor of RPG objects (<c>"Kind": "rpg_object"</c>, contract <c>docs/analysis/rpg-model-v2.md</c>).
    /// The fields of the base window hold the plain values (form, name, category, extends, surface and segment
    /// pictures, charset, facing, column, anchor). Everything else is a block under the preview, written by
    /// <see cref = "Edit"/>: the parts table, the footprint mask, flags, interaction (pickers), states, variants,
    /// lights and the route. The preview shows the object as the game reads it, in the default look, a
    /// variant or a state, and its anchor is dragged on it. A PNG is imported into <c>rpg_images/&lt;category&gt;/</c>.
    /// A foreign object is edited as a patch in the own mod (<c>patches/rpg_object/&lt;mod&gt;.json</c>).
    /// New objects go to <c>objects/&lt;mod&gt;.json</c>. Other properties stay in the JSON box ("other fields").
    /// </summary>
    public partial class RpgObjectEditorWindow : EntityEditorWindow
    {
        private static readonly string[] FormNames =
        {
            "surface",
            "segment",
            "sprite",
            "actor"
        };
        // Set while the base window is built (CreatePreviewBlock), before this constructor's body runs:
        // no initialiser on any field of this class, so the values survive the base constructor.
        private RpgObjectPreview preview;
        private VBoxContainer blocksHost;
        private bool building;
        private string previewChoice;
        private int stateSel;
        private string variantSel;
        private int lightSel;
        /// <summary>Builds the window and opens the first writable mod that has objects.</summary>
        public RpgObjectEditorWindow() : base("tools/rpg_object_editor/title", "rpg_object", "objects", Fields())
        {
            AttachParentRow();
            TreeExiting += CloseVisualPartsWindow;
        }

        /// <summary>The preview block (null before the window is built).</summary>
        public RpgObjectPreview Preview
        {
            get
            {
                return preview;
            }
        }

        private static List<EntityField> Fields()
        {
            return new()
            {
                new EntityField
                {
                    Key = "Form",
                    LabelKey = "tools/rpg_object_editor/form",
                    TooltipKey = "tools/rpg_object_editor/form_tip",
                    Kind = EntityFieldKind.Choice,
                    Options = FormOptions
                },
                new EntityField
                {
                    Key = "Name",
                    LabelKey = "tools/rpg_object_editor/name",
                    Kind = EntityFieldKind.LocaleText,
                    KeyPattern = "{0}/name"
                },
                new EntityField
                {
                    Key = "Category",
                    LabelKey = "tools/rpg_object_editor/category",
                    TooltipKey = "tools/rpg_object_editor/category_tip",
                    Kind = EntityFieldKind.Text
                },
                new EntityField
                {
                    Key = "Extends",
                    LabelKey = "tools/rpg_object_editor/extends",
                    TooltipKey = "tools/rpg_object_editor/extends_tip",
                    Kind = EntityFieldKind.Text
                },
                new EntityField
                {
                    Key = "Surface.Image",
                    LabelKey = "tools/rpg_object_editor/material",
                    TooltipKey = "tools/rpg_object_editor/material_tip",
                    Kind = EntityFieldKind.Picture,
                    Group = RpgTextures.ImageGroup,
                    When = IsSurface
                },
                new EntityField
                {
                    Key = "Surface.DiffuseStrength",
                    LabelKey = "tools/rpg_object_editor/diffuse",
                    TooltipKey = "tools/rpg_object_editor/diffuse_tip",
                    Kind = EntityFieldKind.Integer,
                    When = IsSurface
                },
                new EntityField
                {
                    Key = "Segment.Image",
                    LabelKey = "tools/rpg_object_editor/segment_image",
                    TooltipKey = "tools/rpg_object_editor/segment_image_tip",
                    Kind = EntityFieldKind.Picture,
                    Group = RpgTextures.ImageGroup,
                    When = IsSegment
                },
                new EntityField
                {
                    Key = "Segment.Connect",
                    LabelKey = "tools/rpg_object_editor/connect",
                    TooltipKey = "tools/rpg_object_editor/connect_tip",
                    Kind = EntityFieldKind.Text,
                    When = IsSegment
                },
                new EntityField
                {
                    Key = "Segment.Along",
                    LabelKey = "tools/rpg_object_editor/along",
                    TooltipKey = "tools/rpg_object_editor/along_tip",
                    Kind = EntityFieldKind.Choice,
                    Options = AlongOptions,
                    When = IsSegment
                },
                new EntityField
                {
                    Key = "Anchor",
                    LabelKey = "tools/rpg_object_editor/anchor",
                    TooltipKey = "tools/rpg_object_editor/anchor_tip",
                    Kind = EntityFieldKind.Numbers,
                    When = IsSprite
                },
                new EntityField
                {
                    Key = "Charset",
                    LabelKey = "tools/rpg_object_editor/charset",
                    TooltipKey = "tools/rpg_object_editor/charset_tip",
                    Kind = EntityFieldKind.Picture,
                    Group = RpgTextures.CharsetGroup,
                    When = IsActor
                },
                new EntityField
                {
                    Key = "Facing",
                    LabelKey = "tools/rpg_object_editor/facing",
                    Kind = EntityFieldKind.Choice,
                    Options = FacingOptions,
                    When = IsActor
                },
                new EntityField
                {
                    Key = "Column",
                    LabelKey = "tools/rpg_object_editor/column",
                    Kind = EntityFieldKind.Integer,
                    When = IsActor
                },
                // Blocks of the window edit these (flags, footprint, parts, interaction, states, variants, lights, route).
                // Listed here so the schema of rpg_object does not repeat them as "fields of other mods"; a hidden field is never written.
                Hidden("Solid"),
                Hidden("Water"),
                Hidden("Hidden"),
                Hidden("Footprint"),
                Hidden("DrawOrder"),
                Hidden("Parts"),
                Hidden("Interact"),
                Hidden("Variants"),
                Hidden("Light"),
                Hidden("Lights"),
                Hidden("Route"),
                Hidden("States"),
            };
        }

        private static EntityField Hidden(string key)
        {
            return new()
            {
                Key = key,
                LabelKey = "tools/rpg_object_editor/title",
                Kind = EntityFieldKind.Text,
                When = NeverVisible
            };
        }

        private static bool IsForm(JsonObject data, string form)
        {
            return data != null && string.Equals(EntityPath.Get(data, "Form")?.ToString(), form, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>A new object is a sprite with no picture yet: the author picks one.</summary>
        protected override JsonObject CreateEntity(string id)
        {
            return new()
            {
                ["Id"] = id,
                ["Form"] = "sprite",
                ["Category"] = "misc"
            };
        }

        /// <summary><c>&lt;mod&gt;/new_object</c>, as the schema of <c>rpg_object</c> names new objects.</summary>
        protected override string NewIdBase(string mod)
        {
            return mod + "/new_object";
        }

        /// <summary>
        /// The scenario, map, item and shop of the object's interaction need their mods in depends on save (plan 14.G16).
        /// </summary>
        protected override IEnumerable<(string Kind, string Id)> ExtraReferencesOf(JsonObject data)
        {
            return RpgInteractReferences.Of(data?["Interact"] as JsonObject);
        }

        /// <summary>
        /// The first picture of the object: its first part, else its surface or segment picture; a foreign row passes its
        /// owner's definition.
        /// </summary>
        protected override (string Group, string Path) ThumbnailOf(JsonObject data)
        {
            foreach (var key in new[]
            {
                "Parts.0.Image",
                "Surface.Image",
                "Segment.Image"
            }

            )
            {
                var path = EntityPath.Get(data, key)?.ToString();
                if (!string.IsNullOrEmpty(path))
                {
                    return (RpgTextures.ImageGroup, path);
                }
            }

            return (null, null);
        }

        /// <summary>The preview, and under it the blocks of the object's behaviour and parts.</summary>
        protected override Control CreatePreviewBlock()
        {
            preview = new RpgObjectPreview();
            preview.AnchorDragged += OnAnchorDragged;
            preview.FootprintClicked += OnFootprintClicked;
            preview.NeighbourPicked += OnNeighbourPicked;
            blocksHost = ViewRoot.Create<VBoxContainer>();
            blocksHost.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            blocksHost.Name = "ObjectBlocks";
            blocksHost.AddThemeConstantOverride("separation", UiTokens.Space2);
            var column = ViewRoot.Create<VBoxContainer>();
            column.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            visualPartsButton = ToolUi.Button("tools/rpg_object_editor/visual_parts", OpenVisualParts, ButtonKind.Primary);
            visualPartsButton.Disabled = true;
            column.AddChild(visualPartsButton);
            column.AddChild(preview);
            column.AddChild(blocksHost);
            return column;
        }

        /// <summary>Rebuilds the blocks and the preview for the selected object (the base calls it after each change too).</summary>
        protected override void OnSelectionChanged(EntityRecord record)
        {
            UpdateParentRow(record);
            if (visualPartsButton != null)
            {
                visualPartsButton.Disabled = record?.Data == null || FormOf(record.Data) != "sprite";
            }

            if (blocksHost == null)
            {
                return;
            }

            RebuildBlocks(record);
        }

        // ── writing ────────────────────────────────────────────────
        /// <summary>
        /// Applies one change to the selected object (its root JSON, own or a foreign object's view), records it as an
        /// undo step, writes a foreign object back as a patch and refreshes the form, the blocks and the JSON box.
        /// </summary>
        private void Edit(Action<JsonObject> change)
        {
            if (building)
            {
                return;
            }

            var record = Document?.FindAny(SelectedId);
            if (record?.Data == null)
            {
                return;
            }

            Document.BeginChange();
            change(record.Data);
            if (record.IsForeign)
            {
                Document.CommitForeign(record);
            }

            SetDirty();
            Select(record);
        }

        /// <summary>Every surface material of the game: the dropdown of the ground around the object offers them.</summary>
        private static IEnumerable<string> SurfaceChoices()
        {
            return Entities.Collect<RpgObject>(required: false).Values.Where(IsSurfaceObject).Select(ObjectId);
        }

        private void OnAnchorDragged(float[] anchor)
        {
            Edit(new RpgAnchorChange(anchor).Apply);
        }

        private static JsonObject Obj(JsonObject parent, string key)
        {
            if (parent[key] is JsonObject found)
            {
                return found;
            }

            var created = new JsonObject();
            parent[key] = created;
            return created;
        }

        private static JsonArray Arr(JsonObject parent, string key)
        {
            if (parent[key] is JsonArray found)
            {
                return found;
            }

            var created = new JsonArray();
            parent[key] = created;
            return created;
        }

        private static JsonObject At(JsonArray array, int index)
        {
            while (array.Count <= index)
            {
                array.Add(new JsonObject());
            }

            return array[index].AsObject();
        }

        private static void Set(JsonObject parent, string key, JsonNode value)
        {
            if (value == null)
            {
                parent.Remove(key);
            }
            else
            {
                parent[key] = value;
            }
        }

        private static string Text(JsonObject data, string key)
        {
            return data?[key]?.ToString();
        }

        private static string FormOf(JsonObject data)
        {
            return Text(data, "Form") ?? string.Empty;
        }

        private static RpgPictureRole RoleOf(string form)
        {
            return form switch
            {
                "segment" => RpgPictureRole.Segment,
                "surface" => RpgPictureRole.Surface,
                _ => RpgPictureRole.Part,
            };
        }

        // ── preview ────────────────────────────────────────────────
        private void RenderPreview(EntityRecord record)
        {
            var data = record?.Data;
            if (preview == null || data == null)
            {
                preview?.Clear();
                return;
            }

            try
            {
                var merged = RpgObjectSample.Merged(data, ResolveVisualParent, record.IsForeign);
                var shown = RpgObjectResolve.Effective(merged, null);
                if (previewChoice != null && previewChoice.StartsWith("variant:", StringComparison.Ordinal))
                {
                    var name = previewChoice.Substring("variant:".Length);
                    if (merged.Variants != null && merged.Variants.ContainsKey(name))
                    {
                        shown = RpgObjectResolve.Effective(merged, name);
                    }
                }
                else if (previewChoice != null && previewChoice.StartsWith("state:", StringComparison.Ordinal) 
                    && int.TryParse(previewChoice.Substring("state:".Length), System.Globalization.NumberStyles.Integer,
                     System.Globalization.CultureInfo.InvariantCulture, out var index))
                {
                    shown = RpgObjectResolve.WithState(shown, index);
                }

                preview.Show(shown, SurfaceChoices(), FindPreviewObject, RpgPlayerLook.LeaderCharset());
            }
            catch (JsonException ex)
            {
                preview.Clear();
                GameLogger.Log("TOOLS", $"rpg object editor: '{record.Id}' does not read as an object: {ex.Message}", LogLevel.Error);
            }
        }

        // ── import ─────────────────────────────────────────────────
        /// <summary>
        /// Copies a PNG into <c>rpg_images/&lt;category&gt;/&lt;fileName&gt;.png</c> of the edited mod and points the
        /// object's picture at it (its first part, its surface or its segment). Problems that stop the import are
        /// shown in the status line; grid warnings are shown and the import goes on. Returns the picture id, or null.
        /// </summary>
        public string ImportPicture(string sourcePath, string fileName)
        {
            var record = Document?.FindAny(SelectedId);
            if (record?.Data == null)
            {
                return null;
            }

            var form = FormOf(record.Data);
            if (form == "actor")
            {
                SetStatus(ToolUi.Tr("tools/rpg_object_editor/import_not_for_actor"), ToolStatusKind.Error);
                return null;
            }

            byte[] bytes;
            try
            {
                bytes = File.ReadAllBytes(sourcePath);
            }
            catch (Exception ex)when (ex is IOException or UnauthorizedAccessException)
            {
                SetStatus(string.Format(ToolUi.Tr("tools/rpg_object_editor/import_read"), sourcePath), ToolStatusKind.Error);
                return null;
            }

            var check = RpgPictureImportCheck.Check(bytes, RoleOf(form));
            if (!check.Ok)
            {
                SetStatus(Explain(check.Problems, check), ToolStatusKind.Error);
                return null;
            }

            var id = RpgPictureCheck.TargetId(Text(record.Data, "Category") ?? "misc", fileName);
            if (id == null)
            {
                SetStatus(ToolUi.Tr("tools/rpg_object_editor/import_bad_name"), ToolStatusKind.Error);
                return null;
            }

            var target = ModFileService.GetWritablePath(ModId, "rpg_images/" + id + ".png");
            if (target == null)
            {
                SetStatus(ToolUi.Tr("tools/common/readonly"), ToolStatusKind.Error);
                return null;
            }

            if (File.Exists(target))
            {
                SetStatus(string.Format(ToolUi.Tr("tools/rpg_object_editor/import_exists"), id), ToolStatusKind.Error);
                return null;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(target));
            File.WriteAllBytes(target, bytes);
            ModManager.Instance.GetResolver<FileListResolver>()?.ClearCache();
            GameLogger.Log("TOOLS",
                 $"rpg object editor: imported '{sourcePath}' as rpg_images/{id}.png of '{ModId}' ({check.Width}x{check.Height})");
            var role = RoleOf(form);
            Edit(new RpgPictureChange(role, id).Apply);
            var message = string.Format(ToolUi.Tr("tools/rpg_object_editor/import_done"), id);
            if (check.Warnings.Count > 0)
            {
                message += " " + Explain(check.Warnings, check);
            }

            SetStatus(message, check.Warnings.Count > 0 ? ToolStatusKind.Error : ToolStatusKind.Ok);
            return id;
        }

        internal static void SetPicture(JsonObject root, RpgPictureRole role, string id)
        {
            switch (role)
            {
                case RpgPictureRole.Surface:
                    Obj(root, "Surface")["Image"] = id;
                    break;
                case RpgPictureRole.Segment:
                    Obj(root, "Segment")["Image"] = id;
                    break;
                default:
                    var part = At(Arr(root, "Parts"), 0);
                    part["Id"] ??= "main";
                    part["Image"] = id;
                    break;
            }
        }

        private string Explain(IEnumerable<string> keys, RpgPictureCheck.Result check)
        {
            var messages = new List<string>();
            foreach (var key in keys)
            {
                messages.Add(string.Format(ToolUi.Tr(key), check.Width, check.Height, RpgPictureCheck.DefaultTileSize,
                     RpgPictureCheck.DefaultPatchSize));
            }

            return string.Join(" ", messages);
        }

        private void OpenImportDialog()
        {
            if (Document?.FindAny(SelectedId) == null)
            {
                return;
            }

            var dialog = ViewRoot.Create<FileDialog>();
            dialog.FileMode = FileDialog.FileModeEnum.OpenFile;
            dialog.Access = FileDialog.AccessEnum.Filesystem;
            dialog.Title = ToolUi.Tr("tools/rpg_object_editor/import_title");
            dialog.MinSize = new Vector2I(760, 480);
            dialog.AddFilter("*.png", "PNG");
            dialog.FileSelected += new RpgPictureFileSelection(this, dialog).Selected;
            dialog.Canceled += dialog.QueueFree;
            AddChild(dialog);
            dialog.PopupCentered();
        }

        private static IEnumerable<string> FormOptions()
        {
            return FormNames;
        }

        private static IEnumerable<string> AlongOptions()
        {
            return new[]
            {
                "auto",
                "x",
                "y"
            };
        }

        private static IEnumerable<string> FacingOptions()
        {
            return new[]
            {
                "Down",
                "Left",
                "Right",
                "Up"
            };
        }

        private static bool IsSurface(JsonObject data)
        {
            return IsForm(data, "surface");
        }

        private static bool IsSegment(JsonObject data)
        {
            return IsForm(data, "segment");
        }

        private static bool IsSprite(JsonObject data)
        {
            return IsForm(data, "sprite");
        }

        private static bool IsActor(JsonObject data)
        {
            return IsForm(data, "actor");
        }

        private static bool NeverVisible(JsonObject data)
        {
            return false;
        }

        private void OnNeighbourPicked(string id)
        {
            RenderPreview(Document?.FindAny(SelectedId));
        }

        private static bool IsSurfaceObject(RpgObject value)
        {
            return value.Form == RpgObjectForm.Surface;
        }

        private static string ObjectId(RpgObject value)
        {
            return value.ID;
        }

        private static RpgObject FindPreviewObject(string id)
        {
            return Entities.Collect<RpgObject>().TryGetValue(id, out var found) ? found : null;
        }
    }
}
