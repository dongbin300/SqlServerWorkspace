using SqlServerWorkspace.Enums;

using System.Windows.Media.Imaging;

namespace SqlServerWorkspace.DataModels
{
	public class TreeNode(string name, TreeNodeType type, string path, BitmapImage? iconSource = null)
	{
		public string Name { get; set; } = name;
		public TreeNodeType Type { get; set; } = type;
		public List<TreeNode> Children { get; set; } = [];
		public string Path { get; set; } = path;
		public BitmapImage Icon { get; set; } = iconSource ?? default!;

		public string GetParentName()
		{
			var parts = Path.Split('/', StringSplitOptions.RemoveEmptyEntries);
			return parts.Length < 2 ? string.Empty : parts[^2];
		}
	}
}
