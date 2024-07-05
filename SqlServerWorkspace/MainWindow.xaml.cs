using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;

using SqlServerWorkspace.DataModels;

using System.IO;
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

			LoadDatabaseTree();
			DatabaseTreeView.ItemsSource = databaseNodes;
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
			await ContentsTabControl.CreateNewTab(objectName);

			var text = SqlManager.GetObject(objectName).Replace("\r\n", "\\n").Replace("'", "\\'").Replace("\"", "\\\"");

			await ContentsTabControl.SetEditorText(objectName, text);
		}
	}
}