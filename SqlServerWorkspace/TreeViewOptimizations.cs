using SqlServerWorkspace.Data;
using SqlServerWorkspace.DataModels;
using SqlServerWorkspace.Enums;

using System.Windows;
using System.Windows.Controls;

namespace SqlServerWorkspace
{
	public static class TreeViewOptimizations
	{
		/// <summary>
		/// TreeView에 성능 최적화 Lazy Loading을 적용
		/// </summary>
		public static void OptimizeTreeView(this TreeView treeView)
		{
			// TreeViewItem 확장 이벤트에 Lazy Loading 추가
			treeView.AddHandler(TreeViewItem.ExpandedEvent, new RoutedEventHandler(TreeViewItem_Expanded));
		}

		private static void TreeViewItem_Expanded(object sender, RoutedEventArgs e)
		{
			if (e.OriginalSource is not TreeViewItem item || item.DataContext is not TreeNode node)
				return;

			// 이미 로드된 경우 다시 로드하지 않음
			if (node.Children.Count > 0)
				return;

			try
			{
				switch (node.Type)
				{
					case TreeNodeType.TableTitleNode:
						LoadTables(node);
						break;
					case TreeNodeType.ViewTitleNode:
						LoadViews(node);
						break;
					case TreeNodeType.FunctionTitleNode:
						LoadFunctions(node);
						break;
					case TreeNodeType.ProcedureTitleNode:
						LoadProcedures(node);
						break;
				}
			}
			catch (Exception ex)
			{
				Common.Log($"Error loading tree nodes: {ex.Message}", LogType.Error);
			}
		}

		private static void LoadTables(TreeNode titleNode)
		{
			var databaseName = titleNode.GetParentName();
			var manager = GetSqlManagerForDatabase(databaseName);

			if (manager != null)
			{
				manager.Database = databaseName;
				TreeViewManager.MakeTableTree(manager, titleNode);
			}
		}

		private static void LoadViews(TreeNode titleNode)
		{
			var databaseName = titleNode.GetParentName();
			var manager = GetSqlManagerForDatabase(databaseName);

			if (manager != null)
			{
				manager.Database = databaseName;
				TreeViewManager.MakeViewTree(manager, titleNode);
			}
		}

		private static void LoadFunctions(TreeNode titleNode)
		{
			var databaseName = titleNode.GetParentName();
			var manager = GetSqlManagerForDatabase(databaseName);

			if (manager != null)
			{
				manager.Database = databaseName;
				TreeViewManager.MakeFunctionTree(manager, titleNode);
			}
		}

		private static void LoadProcedures(TreeNode titleNode)
		{
			var databaseName = titleNode.GetParentName();
			var manager = GetSqlManagerForDatabase(databaseName);

			if (manager != null)
			{
				manager.Database = databaseName;
				TreeViewManager.MakeProcedureTree(manager, titleNode);
			}
		}

		private static SqlManager? GetSqlManagerForDatabase(string databaseName)
		{
			return ResourceManager.Connections.FirstOrDefault(c =>
				c.Nodes.Any(n => n.Children.Any(db => db.Name == databaseName)));
		}

		/// <summary>
		/// 특정 데이터베이스의 캐시를 지우고 다시 로드
		/// </summary>
		public static void InvalidateDatabaseCache(string databaseName)
		{
			DatabaseCache.ClearDatabaseCache(databaseName);
		}

		/// <summary>
		/// 모든 캐시를 지우고 다시 로드
		/// </summary>
		public static void InvalidateAllCache()
		{
			DatabaseCache.ClearAllCache();
		}
	}
}