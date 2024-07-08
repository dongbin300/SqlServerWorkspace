using SqlServerWorkspace.DataModels;

using System.IO;
using System.Windows;

namespace SqlServerWorkspace
{
	/// <summary>
	/// Interaction logic for MainWindow.xaml
	/// </summary>
	public partial class MainWindow : Window
	{
		List<TreeNode> databaseNodes = [];

		static string _monacoHtmlPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "index.html");
		static string _userDataFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SqlServerWorkspace");

		public MainWindow()
		{
			InitializeComponent();

			LoadDatabaseTree();
			DatabaseTreeView.ItemsSource = databaseNodes;
		}

		private void DatabaseTreeViewItem_Loaded(object sender, RoutedEventArgs e)
		{
			DatabaseTreeView.ExpandAll(DatabaseTreeView);
		}

		void LoadDatabaseTree()
		{
			var databaseNode = new TreeNode("gaten");
			var tableNode = new TreeNode("Table");
			var viewNode = new TreeNode("View");
			var functionNode = new TreeNode("Function");
			var procedureNode = new TreeNode("Procedure");

			SqlManager.Init("localhost", "gaten");

			var tableNames = SqlManager.SelectTableNames();
			foreach (var tableName in tableNames)
			{
				tableNode.Children.Add(new TreeNode(tableName));
			}
			databaseNode.Children.Add(tableNode);

			var viewNames = SqlManager.SelectViewNames();
			foreach (var viewName in viewNames)
			{
				viewNode.Children.Add(new TreeNode(viewName));
			}
			databaseNode.Children.Add(viewNode);

			var functionNames = SqlManager.SelectFunctionNames();
			foreach (var functionName in functionNames)
			{
				functionNode.Children.Add(new TreeNode(functionName));
			}
			databaseNode.Children.Add(functionNode);

			var procedureNames = SqlManager.SelectProcedureNames();
			foreach (var procedureName in procedureNames)
			{
				procedureNode.Children.Add(new TreeNode(procedureName));
			}
			databaseNode.Children.Add(procedureNode);

			databaseNodes.Add(databaseNode);
		}

		private async void DatabaseTreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
		{
			if (e.NewValue is not TreeNode clickNode)
			{
				return;
			}

			var objectName = clickNode.Name;

			await ContentsTabControl.CreateNewOrOpenTab(objectName);
		}
	}
}