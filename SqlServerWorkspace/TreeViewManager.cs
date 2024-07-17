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
				Common.AppendLogDetail(ex.Message);
			}
		}

		public static void MakeTableTree(SqlManager manager, TreeNode tableTitleNode)
		{
			try
			{
				tableTitleNode.Children.Clear();

				manager.Database = tableTitleNode.GetParentName();
				var tableNames = manager.SelectTableNames();
				foreach (var tableName in tableNames)
				{
					var node = new TreeNode(tableName, TreeNodeType.TableNode, tableTitleNode.Path.CombinePath(tableName), ResourceManager.TableIcon, ResourceManager.TableIconColor);
					tableTitleNode.Children.Add(node);
				}

				tableTitleNode.IsExpanded = true;
			}
			catch (Exception ex)
			{
				Common.AppendLogDetail(ex.Message);
			}
		}

		public static void MakeViewTree(SqlManager manager, TreeNode viewTitleNode)
		{
			try
			{
				viewTitleNode.Children.Clear();

				manager.Database = viewTitleNode.GetParentName();
				var viewNames = manager.SelectViewNames();
				foreach (var viewName in viewNames)
				{
					var node = new TreeNode(viewName, TreeNodeType.ViewNode, viewTitleNode.Path.CombinePath(viewName), ResourceManager.ViewIcon, ResourceManager.ViewIconColor);
					viewTitleNode.Children.Add(node);
				}

				viewTitleNode.IsExpanded = true;
			}
			catch (Exception ex)
			{
				Common.AppendLogDetail(ex.Message);
			}
		}

		public static void MakeFunctionTree(SqlManager manager, TreeNode functionTitleNode)
		{
			try
			{
				functionTitleNode.Children.Clear();

				manager.Database = functionTitleNode.GetParentName();
				var functionNames = manager.SelectFunctionNames();
				foreach (var functionName in functionNames)
				{
					var node = new TreeNode(functionName, TreeNodeType.FunctionNode, functionTitleNode.Path.CombinePath(functionName), ResourceManager.FunctionIcon, ResourceManager.FunctionIconColor);
					functionTitleNode.Children.Add(node);
				}

				functionTitleNode.IsExpanded = true;
			}
			catch (Exception ex)
			{
				Common.AppendLogDetail(ex.Message);
			}
		}

		public static void MakeProcedureTree(SqlManager manager, TreeNode procedureTitleNode)
		{
			try
			{
				procedureTitleNode.Children.Clear();

				manager.Database = procedureTitleNode.GetParentName();
				var procedureNames = manager.SelectProcedureNames();
				foreach (var procedureName in procedureNames)
				{
					var node = new TreeNode(procedureName, TreeNodeType.ProcedureNode, procedureTitleNode.Path.CombinePath(procedureName), ResourceManager.ProcedureIcon, ResourceManager.ProcedureIconColor);
					procedureTitleNode.Children.Add(node);
				}

				procedureTitleNode.IsExpanded = true;
			}
			catch (Exception ex)
			{
				Common.AppendLogDetail(ex.Message);
			}
		}

		public static void AddMenu(this ContextMenu contextMenu, string header, ContextMenuFunction function)
		{
			if(Application.Current.MainWindow is MainWindow mainWindow)
			{
				var menu = new MenuItem { Header = header, Tag = function };
				menu.Click += mainWindow.TreeViewMenuItem_Click;
				contextMenu.Items.Add(menu);
			}
		}
	}
}
