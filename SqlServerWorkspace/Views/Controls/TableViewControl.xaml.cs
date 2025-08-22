using Microsoft.Web.WebView2.Core;

using SqlServerWorkspace.Data;
using SqlServerWorkspace.Enums;
using SqlServerWorkspace.Extensions;

using System.Data;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace SqlServerWorkspace.Views.Controls
{
	/// <summary>
	/// TableViewControl.xaml에 대한 상호 작용 논리
	/// </summary>
	public partial class TableViewControl : UserControl
	{
		public SqlManager Manager { get; set; } = default!;
		public string Header { get; set; } = string.Empty;
		public DataTable SelectTable = default!;
		public string SelectTableName = string.Empty;

		private static readonly char[] separator = [' ', '\t', '\n', '\r'];

		public TableViewControl()
		{
			InitializeComponent();
		}

		private async void UserControl_Loaded(object sender, RoutedEventArgs e)
		{
			await WebView.Init();
		}

		private void ExecuteQuery(string query)
		{
			try
			{
				var table = Manager.Select(query);
				var table2 = table.Copy();
				foreach (DataColumn column in table2.DefaultView.Table?.Columns ?? default!)
				{
					column.AllowDBNull = true;
				}
				SelectTable = table2.Copy();
				SelectTableName = GetTableNameFromQuery(query);
				TableDataGrid.Columns.Clear();
				TableDataGrid.ItemsSource = table2.DefaultView;
				var tableInfo = Manager.GetTableInfo(SelectTableName);

				TableDataGrid.FillSqlDataTable(table, tableInfo);

				Common.Log($"{query}", LogType.Info);
			}
			catch (Exception ex)
			{
				Common.Log(ex.Message, LogType.Error);
			}
		}

		private async void RunButton_Click(object sender, RoutedEventArgs e)
		{
			var query = await WebView.GetEditorText();
			ExecuteQuery(query);
		}

		private void SaveButton_Click(object sender, RoutedEventArgs e)
		{
			try
			{
				var currentTable = TableDataGrid.ToDataTable();
				var result = Manager.TableTransactionV2(SelectTableName, SelectTable, currentTable);

				if (!string.IsNullOrEmpty(result))
				{
					Common.Log(result, LogType.Error);
					return;
				}

				Common.Log("Changes saved successfully.", LogType.Success);
			}
			catch (Exception ex)
			{
				Common.Log(ex.Message, LogType.Error);
			}
		}

		private async void MergeButton_Click(object sender, RoutedEventArgs e)
		{
			var mergeQuery = Manager.GetMergeQuery(SelectTableName);
			await WebView.AppendEditorText($"{Environment.NewLine}{mergeQuery}");

			Common.Log($"{SelectTableName} MERGE", LogType.Info);
		}

		private async void CreateButton_Click(object sender, RoutedEventArgs e)
		{
			var createTableQuery = Manager.GetCreateTableQuery(SelectTableName);
			await WebView.AppendEditorText($"{Environment.NewLine}{createTableQuery}");

			Common.Log($"{SelectTableName} CREATE", LogType.Info);
		}

		private async void CopyButton_Click(object sender, RoutedEventArgs e)
		{
			var inputTableNameView = new NameView
			{
				Owner = Common.MainWindow,
				NameText = ""
			};
			if (inputTableNameView.ShowDialog() ?? false)
			{
				var destinationTableName = inputTableNameView.NameText;
				var copyDataQuery = Manager.GetCopyDataQuery(SelectTableName, destinationTableName);
				await WebView.AppendEditorText($"{Environment.NewLine}{copyDataQuery}");

				Common.Log($"{SelectTableName} -> {destinationTableName} COPY", LogType.Info);
			}
		}

		private async void WebView_NavigationCompleted(object sender, CoreWebView2NavigationCompletedEventArgs e)
		{
			if (e.IsSuccess)
			{
				await WebView.SetEditorText($"SELECT * FROM {Header}");
				var query = await WebView.GetEditorText();
				ExecuteQuery(query);
			}
		}

		private void WebView_KeyDown(object sender, KeyEventArgs e)
		{
			switch (e.Key)
			{
				case Key.F5:
					RunButton_Click(sender, e);
					break;
			}
		}

		private string GetTableNameFromQuery(string query)
		{
			string[] words = query.Split(separator, StringSplitOptions.RemoveEmptyEntries);
			int fromIndex = Array.FindIndex(words, word => word.Equals("from", StringComparison.OrdinalIgnoreCase));

			if (fromIndex != -1 && fromIndex < words.Length - 1)
			{
				return words[fromIndex + 1];
			}

			return string.Empty;
		}

		private void EditButton_Click(object sender, RoutedEventArgs e)
		{
			var view = new TableEditView()
			{
				Manager = Manager,
				TableName = SelectTableName
			};
			if (view.ShowDialog() ?? false)
			{
				Common.RefreshMainWindow();
			}
		}
	}
}
