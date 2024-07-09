using SqlServerWorkspace.Enums;

using System.Windows.Media.Imaging;

namespace SqlServerWorkspace.DataModels
{
	public class TreeNode(string name, TreeNodeType type, BitmapImage? iconSource = null)
	{
		public string Name { get; set; } = name;
		public TreeNodeType Type { get; set; } = type;
		public List<TreeNode> Children { get; set; } = [];
		public BitmapImage Icon { get; set; } = iconSource ?? default!;
	}
}
