using System.Collections.ObjectModel;

namespace TreeViewTest
{
	public class TreeNode
	{
		public string Name { get; set; }
		public ObservableCollection<TreeNode> Children { get; set; }

		public TreeNode(string name)
		{
			Name = name;
			Children = new ObservableCollection<TreeNode>();
		}
	}
}
