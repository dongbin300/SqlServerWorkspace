using AvalonDock.Layout;

using SqlServerWorkspace.Data;
using SqlServerWorkspace.DataModels;
using SqlServerWorkspace.Enums;
using SqlServerWorkspace.Views;

using System.Windows;
using System.Windows.Controls;

namespace SqlServerWorkspace
{
	public class TreeViewContextMenu
	{
		public static void MakeContextMenu(TreeViewItem treeViewItem, TreeNode node)
		{
			var contextMenu = new ContextMenu();

			switch (node.Type)
			{
				case TreeNodeType.DatabaseNode:
					{
						contextMenu.AddMenu("New Query", ContextMenuFunction.NewQuery);
						contextMenu.AddMenu("Refresh", ContextMenuFunction.Refresh);
					}
					break;

				case TreeNodeType.TableTitleNode:
					{
						contextMenu.AddMenu("New Table", ContextMenuFunction.NewTable);
						contextMenu.AddMenu("Refresh", ContextMenuFunction.Refresh);
					}
					break;

				case TreeNodeType.ViewTitleNode:
				case TreeNodeType.FunctionTitleNode:
				case TreeNodeType.ProcedureTitleNode:
					{
						contextMenu.AddMenu("Refresh", ContextMenuFunction.Refresh);
					}
					break;

				case TreeNodeType.TableNode:
					{
						contextMenu.AddMenu("Edit", ContextMenuFunction.Edit);
						contextMenu.AddMenu("Rename", ContextMenuFunction.Rename);
					}
					break;

				case TreeNodeType.ViewNode:
				case TreeNodeType.FunctionNode:
				case TreeNodeType.ProcedureNode:
					{
						contextMenu.AddMenu("Copy", ContextMenuFunction.Copy);
						contextMenu.AddMenu("Rename", ContextMenuFunction.Rename);
						contextMenu.AddMenu("Remove", ContextMenuFunction.Remove);
					}
					break;

				default:
					break;
			}

			contextMenu.PlacementTarget = treeViewItem;
			contextMenu.IsOpen = true;

			contextMenu.Tag = treeViewItem;
		}

		public static async Task ProcessDatabaseNodeMenu(ContextMenuFunction function, TreeNode node, SqlManager manager, LayoutDocumentPane entryPane)
		{
			switch (function)
			{
				case ContextMenuFunction.NewQuery:
					{
						await entryPane.CreateNewOrOpenTab(manager, node);
					}
					break;

				case ContextMenuFunction.Refresh:
					{
						TreeViewManager.MakeDatabaseTree(node);
						Common.RefreshMainWindow();
					}
					break;

				default:
					break;
			}
		}

		public static void ProcessTableTitleNodeMenu(ContextMenuFunction function, TreeNode node, SqlManager manager)
		{
			switch (function)
			{
				case ContextMenuFunction.NewTable:
					{
						manager.Database = node.GetDatabaseName();
						var view = new TableEditView()
						{
							Manager = manager
						};
						if (view.ShowDialog() ?? false)
						{
							Common.RefreshMainWindow();
						}
					}
					break;

				case ContextMenuFunction.Refresh:
					{
						TreeViewManager.MakeTableTree(manager, node);
						Common.RefreshMainWindow();
					}
					break;

				default:
					break;
			}
		}

		public static void ProcessViewTitleNodeMenu(ContextMenuFunction function, TreeNode node, SqlManager manager)
		{
			switch (function)
			{
				case ContextMenuFunction.Refresh:
					{
						TreeViewManager.MakeViewTree(manager, node);
						Common.RefreshMainWindow();
					}
					break;

				default:
					break;
			}
		}

		public static void ProcessFunctionTitleNodeMenu(ContextMenuFunction function, TreeNode node, SqlManager manager)
		{
			switch (function)
			{
				case ContextMenuFunction.Refresh:
					{
						TreeViewManager.MakeFunctionTree(manager, node);
						Common.RefreshMainWindow();
					}
					break;

				default:
					break;
			}
		}

		public static void ProcessProcedureTitleNodeMenu(ContextMenuFunction function, TreeNode node, SqlManager manager)
		{
			switch (function)
			{
				case ContextMenuFunction.Refresh:
					{
						TreeViewManager.MakeProcedureTree(manager, node);
						Common.RefreshMainWindow();
					}
					break;

				default:
					break;
			}
		}

		public static void ProcessTableNodeMenu(ContextMenuFunction function, TreeViewItem item, TreeNode node, SqlManager manager)
		{
			switch (function)
			{
				case ContextMenuFunction.Edit:
					{
						manager.Database = node.GetDatabaseName();
						var view = new TableEditView()
						{
							Manager = manager,
							TableName = node.Name
						};
						if (view.ShowDialog() ?? false)
						{
							Common.RefreshMainWindow();
						}
					}
					break;

				case ContextMenuFunction.Rename:
					{
						var originalName = node.Name;
						var renameView = new NameView
						{
							Owner = Common.MainWindow,
							NameText = originalName
						};
						if (renameView.ShowDialog() ?? false)
						{
							var newName = renameView.NameText;
							var result = manager.Rename(originalName, newName);
							if (!string.IsNullOrEmpty(result))
							{
								Common.Log(result, LogType.Error);
								return;
							}
							Common.Log($"Rename, {originalName} -> {newName}", LogType.Success);

							var parentNode = item.GetParentNode();
							if (parentNode == null)
							{
								return;
							}

							TreeViewManager.MakeTableTree(manager, parentNode);
							Common.RefreshMainWindow();
						}
					}
					break;
				default:
					break;
			}
		}

		public static void ProcessViewNodeMenu(ContextMenuFunction function, TreeViewItem item, TreeNode node, SqlManager manager)
		{
			switch (function)
			{
				case ContextMenuFunction.Rename:
					{
						var originalName = node.Name;
						var renameView = new NameView
						{
							Owner = Common.MainWindow,
							NameText = originalName
						};
						if (renameView.ShowDialog() ?? false)
						{
							var newName = renameView.NameText;
							var result = manager.Rename(originalName, newName);
							if (!string.IsNullOrEmpty(result))
							{
								Common.Log(result, LogType.Error);
								return;
							}
							Common.Log($"Rename, {originalName} -> {newName}", LogType.Success);

							var parentNode = item.GetParentNode();
							if (parentNode == null)
							{
								return;
							}

							TreeViewManager.MakeViewTree(manager, parentNode);
							Common.RefreshMainWindow();
						}
					}
					break;

				case ContextMenuFunction.Copy:
					{
						var originalName = node.Name;
						var copyView = new NameView
						{
							Owner = Common.MainWindow,
							NameText = originalName
						};
						if (copyView.ShowDialog() ?? false)
						{
							var newName = copyView.NameText;
							var result = manager.CopyView(originalName, newName);
							if (!string.IsNullOrEmpty(result))
							{
								Common.Log(result, LogType.Error);
								return;
							}
							Common.Log($"Copy, {originalName} -> {newName}", LogType.Success);
							var parentNode = item.GetParentNode();
							if (parentNode == null)
							{
								return;
							}
							TreeViewManager.MakeViewTree(manager, parentNode);
							Common.RefreshMainWindow();
						}
					}
					break;

				case ContextMenuFunction.Remove:
					{
						if (MessageBox.Show("Are you sure you want to remove this view?", "Remove View", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.No)
						{
							return;
						}
						var result = manager.RemoveView(node.Name);
						if (!string.IsNullOrEmpty(result))
						{
							Common.Log(result, LogType.Error);
							return;
						}
						Common.Log($"Remove, {node.Name}", LogType.Success);
						var parentNode = item.GetParentNode();
						if (parentNode == null)
						{
							return;
						}
						TreeViewManager.MakeViewTree(manager, parentNode);
						Common.RefreshMainWindow();
					}
					break;

				default:
					break;
			}
		}

		public static void ProcessFunctionNodeMenu(ContextMenuFunction function, TreeViewItem item, TreeNode node, SqlManager manager)
		{
			switch (function)
			{
				case ContextMenuFunction.Rename:
					{
						var originalName = node.Name;
						var renameView = new NameView
						{
							Owner = Common.MainWindow,
							NameText = originalName
						};
						if (renameView.ShowDialog() ?? false)
						{
							var newName = renameView.NameText;
							var result = manager.Rename(originalName, newName);
							if (!string.IsNullOrEmpty(result))
							{
								Common.Log(result, LogType.Error);
								return;
							}
							Common.Log($"Rename, {originalName} -> {newName}", LogType.Success);

							var parentNode = item.GetParentNode();
							if (parentNode == null)
							{
								return;
							}

							TreeViewManager.MakeFunctionTree(manager, parentNode);
							Common.RefreshMainWindow();
						}
					}
					break;

				case ContextMenuFunction.Copy:
					{
						var originalName = node.Name;
						var copyView = new NameView
						{
							Owner = Common.MainWindow,
							NameText = originalName
						};
						if (copyView.ShowDialog() ?? false)
						{
							var newName = copyView.NameText;
							var result = manager.CopyFunction(originalName, newName);
							if (!string.IsNullOrEmpty(result))
							{
								Common.Log(result, LogType.Error);
								return;
							}
							Common.Log($"Copy, {originalName} -> {newName}", LogType.Success);
							var parentNode = item.GetParentNode();
							if (parentNode == null)
							{
								return;
							}
							TreeViewManager.MakeFunctionTree(manager, parentNode);
							Common.RefreshMainWindow();
						}
					}
					break;

				case ContextMenuFunction.Remove:
					{
						if (MessageBox.Show("Are you sure you want to remove this function?", "Remove Function", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.No)
						{
							return;
						}
						var result = manager.RemoveFunction(node.Name);
						if (!string.IsNullOrEmpty(result))
						{
							Common.Log(result, LogType.Error);
							return;
						}
						Common.Log($"Remove, {node.Name}", LogType.Success);
						var parentNode = item.GetParentNode();
						if (parentNode == null)
						{
							return;
						}
						TreeViewManager.MakeFunctionTree(manager, parentNode);
						Common.RefreshMainWindow();
					}
					break;

				default:
					break;
			}
		}

		public static void ProcessProcedureNodeMenu(ContextMenuFunction function, TreeViewItem item, TreeNode node, SqlManager manager)
		{
			switch (function)
			{
				case ContextMenuFunction.Rename:
					{
						var originalName = node.Name;
						var renameView = new NameView
						{
							Owner = Common.MainWindow,
							NameText = originalName
						};
						if (renameView.ShowDialog() ?? false)
						{
							var newName = renameView.NameText;
							var result = manager.Rename(originalName, newName);
							if (!string.IsNullOrEmpty(result))
							{
								Common.Log(result, LogType.Error);
								return;
							}
							Common.Log($"Rename, {originalName} -> {newName}", LogType.Success);

							var parentNode = item.GetParentNode();
							if (parentNode == null)
							{
								return;
							}

							TreeViewManager.MakeProcedureTree(manager, parentNode);
							Common.RefreshMainWindow();
						}
					}
					break;

				case ContextMenuFunction.Copy:
					{
						var originalName = node.Name;
						var copyView = new NameView
						{
							Owner = Common.MainWindow,
							NameText = originalName
						};
						if (copyView.ShowDialog() ?? false)
						{
							var newName = copyView.NameText;
							var result = manager.CopyProcedure(originalName, newName);
							if (!string.IsNullOrEmpty(result))
							{
								Common.Log(result, LogType.Error);
								return;
							}
							Common.Log($"Copy, {originalName} -> {newName}", LogType.Success);
							var parentNode = item.GetParentNode();
							if (parentNode == null)
							{
								return;
							}
							TreeViewManager.MakeProcedureTree(manager, parentNode);
							Common.RefreshMainWindow();
						}
					}
					break;

				case ContextMenuFunction.Remove:
					{
						if (MessageBox.Show("Are you sure you want to remove this procedure?", "Remove Procedure", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.No)
						{
							return;
						}

						var result = manager.RemoveProcedure(node.Name);
						if (!string.IsNullOrEmpty(result))
						{
							Common.Log(result, LogType.Error);
							return;
						}
						Common.Log($"Remove, {node.Name}", LogType.Success);
						var parentNode = item.GetParentNode();
						if (parentNode == null)
						{
							return;
						}
						TreeViewManager.MakeProcedureTree(manager, parentNode);
						Common.RefreshMainWindow();
					}
					break;

				default:
					break;
			}
		}
	}
}
