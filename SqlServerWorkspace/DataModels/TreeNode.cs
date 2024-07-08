using SqlServerWorkspace.Enums;

namespace SqlServerWorkspace.DataModels
{
	public class TreeNode(string name, TreeNodeType type, string iconSource)
	{
		public string Name { get; set; } = name;
		public TreeNodeType Type { get; set; } = type;
		public List<TreeNode> Children { get; set; } = [];
		public string IconSource { get; set; } = iconSource;
	}
}
