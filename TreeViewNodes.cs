using System.Collections.Generic;

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
}
