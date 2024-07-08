using System.Windows.Controls;

namespace SqlServerWorkspace
{
	public static class TreeViewManager
	{
		public static void ExpandAll(this TreeView treeView, ItemsControl parent)
		{
			foreach (object item in parent.Items)
			{
				if (parent.ItemContainerGenerator.ContainerFromItem(item) is TreeViewItem treeViewItem)
				{
					treeViewItem.IsExpanded = true;
					ExpandAll(treeView, treeViewItem);
				}
			}
		}

		public static void CollapseAll(this TreeView treeView, ItemsControl parent)
		{
			foreach (object item in parent.Items)
			{
				if (parent.ItemContainerGenerator.ContainerFromItem(item) is TreeViewItem treeViewItem)
				{
					treeViewItem.IsExpanded = false;
					ExpandAll(treeView, treeViewItem);
				}
			}
		}
	}
}
