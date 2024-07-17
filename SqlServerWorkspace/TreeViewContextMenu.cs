using SqlServerWorkspace.Data;
using SqlServerWorkspace.DataModels;
using SqlServerWorkspace.Enums;

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
						contextMenu.AddMenu("Refresh", ContextMenuFunction.Refresh);
					}
					break;

				case TreeNodeType.TableTitleNode:
				case TreeNodeType.ViewTitleNode:
				case TreeNodeType.FunctionTitleNode:
				case TreeNodeType.ProcedureTitleNode:
					{
						contextMenu.AddMenu("Refresh", ContextMenuFunction.Refresh);
					}
					break;

				case TreeNodeType.TableNode:
				case TreeNodeType.ViewNode:
				case TreeNodeType.FunctionNode:
				case TreeNodeType.ProcedureNode:
					break;

				default:
					break;
			}

			contextMenu.PlacementTarget = treeViewItem;
			contextMenu.IsOpen = true;

			contextMenu.Tag = node;
		}

		public static void ProcessDatabaseNodeMenu(ContextMenuFunction function, TreeNode node)
		{
			switch (function)
			{
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

		public static void ProcessTableNodeMenu(ContextMenuFunction function, TreeNode node, SqlManager manager)
		{
			switch (function)
			{
				default:
					break;
			}
		}

		public static void ProcessViewNodeMenu(ContextMenuFunction function, TreeNode node, SqlManager manager)
		{
			switch (function)
			{
				default:
					break;
			}
		}

		public static void ProcessFunctionNodeMenu(ContextMenuFunction function, TreeNode node, SqlManager manager)
		{
			switch (function)
			{
				default:
					break;
			}
		}

		public static void ProcessProcedureNodeMenu(ContextMenuFunction function, TreeNode node, SqlManager manager)
		{
			switch (function)
			{
				default:
					break;
			}
		}
	}
}
