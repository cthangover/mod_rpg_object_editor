namespace Cthangover.Tools.RpgObjectEditor
{
    using System.Text.Json;
    using Cthangover.Mods.Interface.Tool;
    using Cthangover.Mods.Rpg.Validation;
    using Cthangover.Tools.Common;
    using Cthangover.Tools.EntityEditor;

    /// <summary>Validates authored input before DTO setters and the effective family before the shared Save writes anything.</summary>
    public partial class RpgObjectEditorWindow
    {
        /// <summary>
        /// Both button Save and Ctrl+S use this shared preflight. It reads own records and retained foreign patches without
        /// normalizing their data; a refusal preserves all files, unknown fields, the current selection and undo history.
        /// </summary>
        protected override bool ValidateForSave()
        {
            var images = new RpgValidationImages();
            foreach (var record in Document.Records)
            {
                if (!ValidateRecord(record, images))
                {
                    return false;
                }
            }

            foreach (var record in Document.Foreign)
            {
                if (Document.PatchOf(record.Id) != null && !Document.IsDeleted(record) && !ValidateRecord(record, images))
                {
                    return false;
                }
            }

            return true;
        }

        private bool ValidateRecord(EntityRecord record, RpgValidationImages images)
        {
            var source = ModId + "/" + (record.IsForeign ? Document.PatchFile : record.File);
            RpgDataIssue issue = null;
            try
            {
                var projections = new[]
                {
                    "topdown",
                    "isometric",
                };
                foreach (var projection in projections)
                {
                    issue = RpgDataPolicy.FirstBlocking(RpgDataCheck.Authored(record.Data, projection, 0, images.Size));
                    if (issue != null)
                    {
                        break;
                    }

                    var value = RpgObjectSample.Merged(record.Data, ResolveVisualParent, record.IsForeign);
                    if (value == null)
                    {
                        issue = new RpgDataIssue
                        {
                            Code = RpgDataRules.Reference,
                            Field = "$",
                        };
                        break;
                    }

                    issue = RpgDataPolicy.FirstBlocking(RpgObjectDataAudit.Check(value, projection, 0, images, source));
                    if (issue != null)
                    {
                        break;
                    }
                }
            }
            catch (JsonException error)
            {
                issue = new RpgDataIssue
                {
                    Code = RpgDataRules.Json,
                    Field = error.Path ?? "$",
                };
            }

            if (issue == null)
            {
                return true;
            }

            SetStatus(string.Format(ToolUi.Tr("tools/rpg_object_editor/data_save_blocked"),
                record.Id, issue.Field, RpgDataMessages.Explain(issue.Code), source), ToolStatusKind.Error);
            return false;
        }
    }
}
