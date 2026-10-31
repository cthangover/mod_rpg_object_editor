namespace Cthangover.Tools.RpgObjectEditor
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Cthangover.Mods.Interface.Tool;
    using Cthangover.Tools.Common;
    using Cthangover.Tools.Services;
    using Godot;

    /// <summary>
    /// Picks one id of a list (scenarios, maps) with a search: a double click or OK picks it. The dialog reports
    /// the id through <see cref="Picked"/> and closes; <see cref="Choose"/> does the same for code and tests.
    /// </summary>
    public partial class RpgChoicePickerDialog : AcceptDialog
    {
        private readonly FilteredList list;

        /// <summary>An id was picked.</summary>
        public event Action<string> Picked;

        /// <summary>Builds the dialog for <paramref name="ids"/>, with <paramref name="current"/> selected when it is in the list.</summary>
        public RpgChoicePickerDialog(string title, IEnumerable<string> ids, string current)
        {
            Theme = ToolTheme.Get();
            Title = title;
            MinSize = new Vector2I(520, 480);
            list = new FilteredList(360f);
            list.SetItems(ids.ToList());
            if (current != null)
                list.Select(current);
            list.Activated += Choose;
            AddChild(list);
            Confirmed += () =>
            {
                if (list.SelectedId != null)
                    Choose(list.SelectedId);
            };
            CloseRequested += QueueFree;
            Canceled += QueueFree;
        }

        /// <summary>Picks <paramref name="id"/>: reports it and closes the dialog.</summary>
        public void Choose(string id)
        {
            if (string.IsNullOrEmpty(id))
                return;
            Picked?.Invoke(id);
            Hide();
            QueueFree();
        }
    }
}
