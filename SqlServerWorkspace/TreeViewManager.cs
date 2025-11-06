using SqlServerWorkspace.Data;
using SqlServerWorkspace.DataModels;
using SqlServerWorkspace.Enums;
using SqlServerWorkspace.Extensions;

using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

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

		public static TreeViewItem? FindTreeViewItem(ItemsControl parent, string path)
		{
			foreach (var item in parent.Items)
			{
				if (parent.ItemContainerGenerator.ContainerFromItem(item) is not TreeViewItem treeViewItem)
				{
					continue;
				}
				if (treeViewItem.Tag?.ToString() == path)
				{
					return treeViewItem;
				}
				var foundChild = FindTreeViewItem(treeViewItem, path);
				if (foundChild != null)
				{
					return foundChild;
				}
			}
			return null;
		}

		public static TreeViewItem? FindTopLevelNode(TreeViewItem node)
		{
			if (node == null)
			{
				return null;
			}

			var parent = GetParentTreeViewItem(node);
			return parent == null ? node : FindTopLevelNode(parent);
		}

		public static List<TreeViewItem> GetAllTopLevelNodes(this TreeView treeView)
		{
			var nodes = new List<TreeViewItem>();
			foreach (var item in treeView.Items)
			{
				if (treeView.ItemContainerGenerator.ContainerFromItem(item) is TreeViewItem treeViewItem)
				{
					nodes.Add(treeViewItem);
				}
			}
			return nodes;
		}

		public static TreeViewItem? GetParentTreeViewItem(TreeViewItem item)
		{
			var parent = VisualTreeHelper.GetParent(item);
			while (parent != null && parent is not TreeViewItem)
			{
				parent = VisualTreeHelper.GetParent(parent);
			}
			return parent as TreeViewItem;
		}

		public static TreeViewItem? GetParent(this TreeViewItem item)
		{
			if (item.Parent is not TreeViewItem parentItem)
			{
				return null;
			}
			return parentItem;
		}

		public static TreeNode? GetParentNode(this TreeViewItem item)
		{
			var parent = item.GetParent();
			if (parent?.Header is not TreeNode node)
			{
				return parent?.GetNode();
			}
			return node;
		}

		public static TreeNode? GetNode(this TreeViewItem item)
		{
			if (item.DataContext is not TreeNode node)
			{
				if (item.DataContext is not List<TreeNode> nodes)
				{
					return null;
				}
				return nodes[0]; // Server node
			}
			return node; // Not server node
		}

		public static TreeViewItem? GetTreeViewItemByNode(this TreeView treeView, TreeNode node)
		{
			for (int i = 0; i < treeView.Items.Count; i++)
			{
				if (treeView.ItemContainerGenerator.ContainerFromIndex(i) is TreeViewItem container)
				{
					var found = container.GetTreeViewItemRecursive(node);
					if (found != null)
					{
						return found;
					}
				}
			}
			return null;
		}

		public static TreeViewItem? GetTreeViewItemRecursive(this TreeViewItem item, TreeNode node)
		{
			if (item.DataContext == node || (item.DataContext is TreeNode itemNode && itemNode == node))
			{
				return item;
			}

			for (int i = 0; i < item.Items.Count; i++)
			{
				if (item.ItemContainerGenerator.ContainerFromIndex(i) is TreeViewItem childContainer)
				{
					var found = childContainer.GetTreeViewItemRecursive(node);
					if (found != null)
					{
						return found;
					}
				}
			}
			return null;
		}

		public static void MakeDatabaseTree(TreeNode databaseNode)
		{
			try
			{
				databaseNode.Children.Clear();

				var tableNode = new TreeNode("Table", TreeNodeType.TableTitleNode, databaseNode.Path.CombinePath("Table"), ResourceManager.TableIcon, ResourceManager.TableIconColor);
				var viewNode = new TreeNode("View", TreeNodeType.ViewTitleNode, databaseNode.Path.CombinePath("View"), ResourceManager.ViewIcon, ResourceManager.ViewIconColor);
				var functionNode = new TreeNode("Function", TreeNodeType.FunctionTitleNode, databaseNode.Path.CombinePath("Function"), ResourceManager.FunctionIcon, ResourceManager.FunctionIconColor);
				var procedureNode = new TreeNode("Procedure", TreeNodeType.ProcedureTitleNode, databaseNode.Path.CombinePath("Procedure"), ResourceManager.ProcedureIcon, ResourceManager.ProcedureIconColor);
				databaseNode.Children.Add(tableNode);
				databaseNode.Children.Add(viewNode);
				databaseNode.Children.Add(functionNode);
				databaseNode.Children.Add(procedureNode);

				databaseNode.IsExpanded = true;
			}
			catch (Exception ex)
			{
				Common.Log(ex.Message, LogType.Error);
			}
		}

		public static void MakeTableTree(SqlManager manager, TreeNode tableTitleNode)
		{
			try
			{
				var databaseName = tableTitleNode.GetParentName();
				var parentNode = manager.Nodes[0].Children.First(x => x.Name.Equals(databaseName)).Children.First(x => x.Name.Equals(tableTitleNode.Name));
				manager.Database = databaseName;

				parentNode.Children.Clear();
				var tableNames = manager.SelectTableNames();
				foreach (var tableName in tableNames)
				{
					var node = new TreeNode(tableName, TreeNodeType.TableNode, parentNode.Path.CombinePath(tableName), ResourceManager.TableIcon, ResourceManager.TableIconColor);
					parentNode.Children.Add(node);
				}

				parentNode.IsExpanded = true;
			}
			catch (Exception ex)
			{
				Common.Log(ex.Message, LogType.Error);
			}
		}

		public static void MakeViewTree(SqlManager manager, TreeNode viewTitleNode)
		{
			try
			{
				var databaseName = viewTitleNode.GetParentName();
				var parentNode = manager.Nodes[0].Children.First(x => x.Name.Equals(databaseName)).Children.First(x => x.Name.Equals(viewTitleNode.Name));
				manager.Database = databaseName;

				parentNode.Children.Clear();
				var viewNames = manager.SelectViewNames();
				foreach (var viewName in viewNames)
				{
					var node = new TreeNode(viewName, TreeNodeType.ViewNode, parentNode.Path.CombinePath(viewName), ResourceManager.ViewIcon, ResourceManager.ViewIconColor);
					parentNode.Children.Add(node);
				}

				parentNode.IsExpanded = true;
			}
			catch (Exception ex)
			{
				Common.Log(ex.Message, LogType.Error);
			}
		}

		public static void MakeFunctionTree(SqlManager manager, TreeNode functionTitleNode)
		{
			try
			{
				var databaseName = functionTitleNode.GetParentName();
				var parentNode = manager.Nodes[0].Children.First(x => x.Name.Equals(databaseName)).Children.First(x => x.Name.Equals(functionTitleNode.Name));
				manager.Database = databaseName;

				parentNode.Children.Clear();
				var functionNames = manager.SelectFunctionNames();
				foreach (var functionName in functionNames)
				{
					var node = new TreeNode(functionName, TreeNodeType.FunctionNode, parentNode.Path.CombinePath(functionName), ResourceManager.FunctionIcon, ResourceManager.FunctionIconColor);
					parentNode.Children.Add(node);
				}

				parentNode.IsExpanded = true;
			}
			catch (Exception ex)
			{
				Common.Log(ex.Message, LogType.Error);
			}
		}

		public static void MakeProcedureTree(SqlManager manager, TreeNode procedureTitleNode)
		{
			try
			{
				var databaseName = procedureTitleNode.GetParentName();
				var parentNode = manager.Nodes[0].Children.First(x => x.Name.Equals(databaseName)).Children.First(x => x.Name.Equals(procedureTitleNode.Name));
				manager.Database = databaseName;

				parentNode.Children.Clear();
				var procedureNames = manager.SelectProcedureNames();
				foreach (var procedureName in procedureNames)
				{
					var node = new TreeNode(procedureName, TreeNodeType.ProcedureNode, parentNode.Path.CombinePath(procedureName), ResourceManager.ProcedureIcon, ResourceManager.ProcedureIconColor);
					parentNode.Children.Add(node);
				}

				parentNode.IsExpanded = true;
			}
			catch (Exception ex)
			{
				Common.Log(ex.Message, LogType.Error);
			}
		}

		public static void AddMenu(this ContextMenu contextMenu, string header, ContextMenuFunction function)
		{
			if (Application.Current.MainWindow is MainWindow mainWindow)
			{
				var menu = new MenuItem { Header = header, Tag = function };
				menu.Click += mainWindow.TreeViewMenuItem_Click;
				contextMenu.Items.Add(menu);
			}
		}
	}
}
