using SqlServerWorkspace.Data;
using SqlServerWorkspace.DataModels;
using SqlServerWorkspace.Enums;
using SqlServerWorkspace.Extensions;

using System.IO;
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

		public static TreeViewItem? GetParentTreeViewItem(TreeViewItem item)
		{
			var parent = VisualTreeHelper.GetParent(item);
			while (parent != null && parent is not TreeViewItem)
			{
				parent = VisualTreeHelper.GetParent(parent);
			}
			return parent as TreeViewItem;
		}

		public static void MakeDatabaseTree(TreeNode databaseNode)
		{
			var tableNode = new TreeNode("Table", TreeNodeType.TableTitleNode, databaseNode.Path.CombinePath("Table"));
			var viewNode = new TreeNode("View", TreeNodeType.ViewTitleNode, databaseNode.Path.CombinePath("View"));
			var functionNode = new TreeNode("Function", TreeNodeType.FunctionTitleNode, databaseNode.Path.CombinePath("Function"), ResourceManager.FunctionIcon);
			var procedureNode = new TreeNode("Procedure", TreeNodeType.ProcedureTitleNode, databaseNode.Path.CombinePath("Procedure"));
			databaseNode.Children.Add(tableNode);
			databaseNode.Children.Add(viewNode);
			databaseNode.Children.Add(functionNode);
			databaseNode.Children.Add(procedureNode);
		}

		public static void MakeTableTree(SqlManager manager, TreeNode tableTitleNode)
		{
			manager.Database = tableTitleNode.GetParentName();
			var tableNames = manager.SelectTableNames();
			foreach (var tableName in tableNames)
			{
				tableTitleNode.Children.Add(new TreeNode(tableName, TreeNodeType.TableNode, tableTitleNode.Path.CombinePath(tableName)));
			}
		}

		public static void MakeViewTree(SqlManager manager, TreeNode viewTitleNode)
		{
			manager.Database = viewTitleNode.GetParentName();
			var viewNames = manager.SelectViewNames();
			foreach (var viewName in viewNames)
			{
				viewTitleNode.Children.Add(new TreeNode(viewName, TreeNodeType.ViewNode, viewTitleNode.Path.CombinePath(viewName)));
			}
		}

		public static void MakeFunctionTree(SqlManager manager, TreeNode functionTitleNode)
		{
			manager.Database = functionTitleNode.GetParentName();
			var functionNames = manager.SelectFunctionNames();
			foreach (var functionName in functionNames)
			{
				functionTitleNode.Children.Add(new TreeNode(functionName, TreeNodeType.FunctionNode, functionTitleNode.Path.CombinePath(functionName)));
			}
		}

		public static void MakeProcedureTree(SqlManager manager, TreeNode procedureTitleNode)
		{
			manager.Database = procedureTitleNode.GetParentName();
			var procedureNames = manager.SelectProcedureNames();
			foreach (var procedureName in procedureNames)
			{
				procedureTitleNode.Children.Add(new TreeNode(procedureName, TreeNodeType.ProcedureNode, procedureTitleNode.Path.CombinePath(procedureName)));
			}
		}
	}
}
