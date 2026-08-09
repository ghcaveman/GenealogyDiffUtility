using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace GenealogyDiffUtility
{
    // A generic node for categories that holds children and a display name
    internal class TreeGroupNode : TreeNodeBase
    {
        public string Key { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public List<object> Children { get; set; } = new();
    }

    // A specific group node for Surnames
    internal class SurnameGroupNode : TreeNodeBase
    {
        public string Surname { get; set; } = string.Empty;
        public string DisplayName => $"{Surname} ({People.Count})";
        public List<IndividualNode> People { get; set; } = new();
    }

    /// <summary>
    /// A lightweight display leaf shown beneath an <see cref="IndividualNode"/> when
    /// the "Details" view is active. It surfaces the spouses, children, and notes
    /// associated with a person without participating in cross-tree sync.
    /// </summary>
    internal class IndividualDetailNode : TreeNodeBase
    {
        public string Role { get; set; } = string.Empty;   // "Spouse", "Child", "Note"
        public string DisplayName { get; set; } = string.Empty;
    }

    /// <summary>
    /// A lightweight display leaf shown beneath a <see cref="NoteNode"/> when
    /// the "Details" view is active. It surfaces the records (Individual, Family,
    /// Source, Repository, etc.) that reference the note, without participating
    /// in cross-tree sync.
    /// </summary>
    internal class NoteDetailNode : TreeNodeBase
    {
        public string Role { get; set; } = string.Empty;   // "Individual", "Family", "Source", "Repository"
        public string DisplayName { get; set; } = string.Empty;
    }

    /// <summary>
    /// A lightweight display leaf shown beneath a <see cref="RepositoryNode"/> when
    /// the "Details" view is active. It surfaces the records (Source, etc.) that
    /// reference the repository, without participating in cross-tree sync.
    /// </summary>
    internal class RepositoryDetailNode : TreeNodeBase
    {
        public string Role { get; set; } = string.Empty;   // "Source"
        public string DisplayName { get; set; } = string.Empty;
    }

    /// <summary>
    /// A lightweight display leaf shown beneath a <see cref="SourceNode"/> when
    /// the "Details" view is active. It surfaces the records (Individual, Family,
    /// etc.) that reference the source — either directly or via one of their
    /// events — without participating in cross-tree sync.
    /// </summary>
    internal class SourceDetailNode : TreeNodeBase
    {
        public string Role { get; set; } = string.Empty;   // "Individual", "Family"
        public string DisplayName { get; set; } = string.Empty;
    }

    /// <summary>
    /// A lightweight display leaf shown beneath a <see cref="FamilyNode"/> when
    /// the "Details" view is active. It surfaces the spouses and children
    /// associated with a family without participating in cross-tree sync.
    /// </summary>
    internal class FamilyDetailNode : TreeNodeBase
    {
        public string Role { get; set; } = string.Empty;   // "Spouse", "Child"
        public string DisplayName { get; set; } = string.Empty;
    }

    /// <summary>
    /// A lightweight display node shown beneath an <see cref="IndividualNode"/> or
    /// <see cref="FamilyNode"/> when the "Details" view is active. It surfaces the
    /// events (birth, death, marriage, census, residence, etc.) associated with
    /// the record, without participating in cross-tree sync. Each event can
    /// optionally expand to show the sources that prove it.
    /// </summary>
    internal class EventDetailNode : TreeNodeBase
    {
        public string Role { get; set; } = string.Empty;   // "Event"
        public string DisplayName { get; set; } = string.Empty;

        /// <summary>
        /// The sources that cite this event, shown as expandable children when
        /// the event has any citations. These are display-only leaves and do not
        /// participate in cross-tree sync.
        /// </summary>
        public ObservableCollection<TreeNodeBase> Sources { get; } = new();
    }

    /// <summary>
    /// A lightweight display leaf shown beneath an <see cref="EventDetailNode"/>
    /// when the "Details" view is active. It surfaces a single source that cites
    /// the event, without participating in cross-tree sync.
    /// </summary>
    internal class EventSourceDetailNode : TreeNodeBase
    {
        public string Role { get; set; } = "Source";
        public string DisplayName { get; set; } = string.Empty;
    }
}
