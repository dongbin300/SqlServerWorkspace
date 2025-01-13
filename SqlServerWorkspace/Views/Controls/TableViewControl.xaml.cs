using Microsoft.Web.WebView2.Core;

using SqlServerWorkspace.Data;
using SqlServerWorkspace.Enums;
using SqlServerWorkspace.Extensions;

using System.Data;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Xml.Linq;

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
				SelectTable = table.Copy();
				SelectTableName = GetTableNameFromQuery(query);
				TableDataGrid.Columns.Clear();
				TableDataGrid.ItemsSource = table.DefaultView;
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

		private void TableDataGrid_MouseRightButtonUp(object sender, MouseButtonEventArgs e)
		{
			if (TableDataGrid.SelectedItem == null)
			{
				return;
			}

			var contextMenu = new ContextMenu();

			var copyMenuItem = new MenuItem { Header = "Copy" };
			copyMenuItem.Click += CopyMenuItem_Click;
			contextMenu.Items.Add(copyMenuItem);

			var pasteMenuItem = new MenuItem { Header = "Paste" };
			pasteMenuItem.Click += PasteMenuItem_Click;
			if (string.IsNullOrEmpty(Clipboard.GetText()))
			{
				pasteMenuItem.IsEnabled = false;
			}
			contextMenu.Items.Add(pasteMenuItem);

			var duplicateMenuItem = new MenuItem { Header = "Duplicate" };
			duplicateMenuItem.Click += DuplicateMenuItem_Click;
			contextMenu.Items.Add(duplicateMenuItem);

			contextMenu.IsOpen = true;
		}

		private void CopyMenuItem_Click(object sender, RoutedEventArgs e)
		{
			if (TableDataGrid.ItemsSource is not DataView || TableDataGrid.SelectedItem == null)
			{
				return;
			}

			if (TableDataGrid.SelectedItem is not DataRowView dataRowView)
			{
				return;
			}

			var selectedRow = dataRowView.Row;
			var text = string.Join('\t', selectedRow.ItemArray);
			Clipboard.SetText(text);
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

		private void PasteMenuItem_Click(object sender, RoutedEventArgs e)
		{
			if (TableDataGrid.ItemsSource is not DataView || TableDataGrid.SelectedItem == null)
			{
				return;
			}

			if (TableDataGrid.SelectedItem is not DataRowView dataRowView)
			{
				return;
			}

			var selectedRow = dataRowView.Row;
			var clipboardText = Clipboard.GetText();

			if (string.IsNullOrEmpty(clipboardText))
			{
				return;
			}

			var values = clipboardText.Split('\t');

			for (int i = 0; i < values.Length && i < selectedRow.Table.Columns.Count; i++)
			{
				if (string.IsNullOrWhiteSpace(values[i]))
				{
					continue;
				}

				var column = selectedRow.Table.Columns[i];

				if (column.DataType == typeof(int) && int.TryParse(values[i], out int intValue))
				{
					selectedRow[i] = intValue;
				}
				else if (column.DataType == typeof(double) && double.TryParse(values[i], out double doubleValue))
				{
					selectedRow[i] = doubleValue;
				}
				else if (column.DataType == typeof(DateTime) && DateTime.TryParse(values[i], out DateTime dateTimeValue))
				{
					selectedRow[i] = dateTimeValue;
				}
				else
				{
					selectedRow[i] = values[i];
				}
			}
		}

		private void DuplicateMenuItem_Click(object sender, RoutedEventArgs e)
		{
			if (TableDataGrid.ItemsSource is not DataView dataView || TableDataGrid.SelectedItem == null)
			{
				return;
			}

			if (TableDataGrid.SelectedItem is not DataRowView dataRowView)
			{
				return;
			}

			var selectedRow = dataRowView.Row;

			if (dataView.Table == null)
			{
				return;
			}

			var newRow = dataView.Table.NewRow();

			if (selectedRow.ItemArray.Clone() is not object[] itemArray)
			{
				return;
			}

			newRow.ItemArray = itemArray;

			int selectedIndex = TableDataGrid.SelectedIndex;
			dataView.Table.Rows.InsertAt(newRow, selectedIndex + 1);
		}

		private void TableDataGrid_KeyDown(object sender, KeyEventArgs e)
		{
			if (e.Key == Key.V && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
			{
				PasteClipboardContent();
				e.Handled = true;
			}
		}

		/// <summary>
		/// 테이블 데이터를 복사 붙여넣기로 간단 입력
		/// 행은 줄바꿈, 열은 탭으로 구분
		/// </summary>
		private void PasteClipboardContent()
		{
			try
			{
				string clipboardText = Clipboard.GetText();

				string[] lines = clipboardText.Split(["\r\n", "\n"], StringSplitOptions.RemoveEmptyEntries);

				var selectedCell = TableDataGrid.SelectedCells.FirstOrDefault();

				int startRowIndex = TableDataGrid.Items.IndexOf(selectedCell.Item);
				int startColIndex = selectedCell.Column.DisplayIndex;

				for (int i = 0; i < lines.Length; i++)
				{
					int rowIndex = startRowIndex + i;
					if (rowIndex >= TableDataGrid.Items.Count) break;

					var row = TableDataGrid.Items[rowIndex];
					var values = lines[i].Split('\t');

					for (int j = 0; j < values.Length; j++)
					{
						int colIndex = startColIndex + j;
						if (colIndex >= TableDataGrid.Columns.Count) break;

						var column = TableDataGrid.Columns[colIndex];
						column.OnPastingCellClipboardContent(row, values[j]);
					}
				}
			}
			catch (Exception ex)
			{
				MessageBox.Show(ex.Message);
			}
		}

		private void TableDataGrid_LoadingRow(object sender, DataGridRowEventArgs e)
		{
			e.Row.Header = (e.Row.GetIndex() + 1).ToString();
		}
    }
}
