using SqlServerWorkspace.DataModels;
using SqlServerWorkspace.Enums;

using System.Windows;
using System.Windows.Controls;

namespace SqlServerWorkspace
{
	/// <summary>
	/// Interaction logic for MainWindow.xaml
	/// </summary>
	public partial class MainWindow : Window
	{
		List<TreeNode> databaseNodes = [];

		public MainWindow()
		{
			InitializeComponent();

			ResourceManager.Init();

			LoadDatabaseTree();
			DatabaseTreeView.ItemsSource = databaseNodes;
		}

		private void DatabaseTreeViewItem_Loaded(object sender, RoutedEventArgs e)
		{
			DatabaseTreeView.ExpandAll(DatabaseTreeView);
		}

		void LoadDatabaseTree()
		{
			var databaseNode = new TreeNode("gaten", TreeNodeType.DatabaseNode);

			var tableNode = new TreeNode("Table", TreeNodeType.TableTitleNode);
			var viewNode = new TreeNode("View", TreeNodeType.ViewTitleNode);
			var functionNode = new TreeNode("Function", TreeNodeType.FunctionTitleNode, ResourceManager.FunctionIcon);
			var procedureNode = new TreeNode("Procedure", TreeNodeType.ProcedureTitleNode);

			SqlManager.Init("localhost", "gaten");

			var tableNames = SqlManager.SelectTableNames();
			foreach (var tableName in tableNames)
			{
				tableNode.Children.Add(new TreeNode(tableName, TreeNodeType.TableNode));
			}
			databaseNode.Children.Add(tableNode);

			var viewNames = SqlManager.SelectViewNames();
			foreach (var viewName in viewNames)
			{
				viewNode.Children.Add(new TreeNode(viewName, TreeNodeType.ViewNode));
			}
			databaseNode.Children.Add(viewNode);

			var functionNames = SqlManager.SelectFunctionNames();
			foreach (var functionName in functionNames)
			{
				functionNode.Children.Add(new TreeNode(functionName, TreeNodeType.FunctionNode, ResourceManager.FunctionIcon));
			}
			databaseNode.Children.Add(functionNode);

			var procedureNames = SqlManager.SelectProcedureNames();
			foreach (var procedureName in procedureNames)
			{
				procedureNode.Children.Add(new TreeNode(procedureName, TreeNodeType.ProcedureNode));
			}
			databaseNode.Children.Add(procedureNode);

			databaseNodes.Add(databaseNode);
		}

		private void DatabaseTreeViewItem_Selected(object sender, RoutedEventArgs e)
		{
			if (sender is not TreeViewItem item)
			{
				return;
			}

			if (item.DataContext is not TreeNode treeNode)
			{
				return;
			}

			switch (treeNode.Type)
			{
				case TreeNodeType.DatabaseNode:
				case TreeNodeType.TableTitleNode:
				case TreeNodeType.ViewTitleNode:
				case TreeNodeType.FunctionTitleNode:
				case TreeNodeType.ProcedureTitleNode:
					item.IsExpanded = !item.IsExpanded;
					break;

				default:
					break;
			}

			item.IsSelected = false;
		}

		private async void DatabaseTreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
		{
			if (e.NewValue is not TreeNode treeNode)
			{
				return;
			}

			switch (treeNode.Type)
			{
				case TreeNodeType.TableNode:
				case TreeNodeType.ViewNode:
				case TreeNodeType.FunctionNode:
				case TreeNodeType.ProcedureNode:
					await ContentsTabControl.CreateNewOrOpenTab(treeNode);
					break;

				default:
					break;
			}
		}
	}
}