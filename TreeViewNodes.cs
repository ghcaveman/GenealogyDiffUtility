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
}