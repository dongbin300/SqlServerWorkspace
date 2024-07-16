using Microsoft.Web.WebView2.Core;

using SqlServerWorkspace.Data;
using SqlServerWorkspace.Extensions;

using System.Data;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
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
				SelectTable = table.Copy();
				SelectTableName = GetTableNameFromQuery(query);
				TableDataGrid.Columns.Clear();
				TableDataGrid.ItemsSource = table.DefaultView;

				foreach (DataColumn column in table.Columns)
				{
					var binding = new Binding(column.ColumnName)
					{
						//Converter = (IValueConverter)Application.Current.FindResource("DBNullToNullStringConverter")
					};

					if (column.DataType == typeof(DateTime))
					{
						binding.StringFormat = "yyyy-MM-dd HH:mm:ss";
					}

					var dataGridColumn = new DataGridTextColumn
					{
						Header = column.ColumnName,
						Binding = binding
					};

					//var headerTemplate = new DataTemplate();
					//var textBlockFactory = new FrameworkElementFactory(typeof(TextBlock));
					//textBlockFactory.SetBinding(TextBlock.TextProperty, new Binding());
					//headerTemplate.VisualTree = textBlockFactory;
					//dataGridColumn.HeaderTemplate = headerTemplate;

					TableDataGrid.Columns.Add(dataGridColumn);
				}

				//var rowNumberColumn = new DataGridTextColumn
				//{
				//	Header = "#",
				//	Binding = new Binding
				//	{
				//		Path = new PropertyPath("Items.IndexOf"),
				//		RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor) { AncestorType = typeof(DataGridRow) }
				//	}
				//};
				//dataGrid.Columns.Insert(0, rowNumberColumn);

				Common.SetStatusText("Complete");
			}
			catch (Exception ex)
			{
				Common.SetStatusText(ex.Message);
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
				// DataGrid 테이블
				//var currentTable = TableDataGrid.ToDataTable();

				//// 조회해놓은 테이블
				//var selectTable2 = new DataTable();
				//foreach (DataColumn column in SelectTable.Columns)
				//{
				//	selectTable2.Columns.Add(column.ColumnName, typeof(string));
				//}

				//foreach (DataRow row in SelectTable.Rows)
				//{
				//	DataRow newRow = selectTable2.NewRow();
				//	foreach (DataColumn column in selectTable2.Columns)
				//	{
				//		if (row[column.ColumnName] == DBNull.Value)
				//		{
				//			continue;
				//		}
				//		newRow[column.ColumnName] = row[column.ColumnName].ToString();
				//	}
				//	selectTable2.Rows.Add(newRow);
				//}

				//var addedRows = currentTable.AsEnumerable().Except(selectTable2.AsEnumerable(), DataRowComparer.Default);
				//var deletedRows = selectTable2.AsEnumerable().Except(currentTable.AsEnumerable(), DataRowComparer.Default);
				//var modifiedRows = currentTable.AsEnumerable().Where(row => !selectTable2.AsEnumerable().Contains(row, DataRowComparer.Default) && !addedRows.Contains(row, DataRowComparer.Default));

				var currentTable = TableDataGrid.ToDataTable();
				var selectTable2 = new DataTable();
				foreach (DataColumn column in SelectTable.Columns)
				{
					selectTable2.Columns.Add(column.ColumnName, typeof(string));
				}

				foreach (DataRow row in SelectTable.Rows)
				{
					DataRow newRow = selectTable2.NewRow();
					foreach (DataColumn column in selectTable2.Columns)
					{
						if (row[column.ColumnName] == DBNull.Value)
						{
							continue;
						}
						newRow[column.ColumnName] = row[column.ColumnName].ToString();
					}
					selectTable2.Rows.Add(newRow);
				}

				List<DataRow> addedRows = [];
				List<DataRow> deletedRows = [];
				List<DataRow> modifiedRows = [];
				foreach (DataRow currentRow in currentTable.Rows)
				{
					object primaryKeyValue = currentRow["ID"];
					var selectRow = selectTable2.Rows.Find(primaryKeyValue);

					if (selectRow == null)
					{
						addedRows.Add(currentRow);
					}
					else
					{
						bool hasChanges = false;
						foreach (DataColumn column in currentTable.Columns)
						{
							if (!Equals(currentRow[column], selectRow[column]))
							{
								hasChanges = true;
								break;
							}
						}

						if (hasChanges)
						{
							modifiedRows.Add(currentRow);
						}
						else
						{
							Console.WriteLine($"Unchanged Row: {currentRow["ID"]} - {currentRow["Name"]}");
						}
					}
				}

				foreach (DataRow selectRow in selectTable2.Rows)
				{
					object primaryKeyValue = selectRow["ID"];
					var currentRow = currentTable.Rows.Find(primaryKeyValue);

					if (currentRow == null)
					{
						deletedRows.Add(selectRow);
					}
				}

				var result = Manager.TransactionForTable(SelectTableName, addedRows, deletedRows, modifiedRows);

				Common.SetStatusText(result);
			}
			catch (Exception ex)
			{
				Common.SetStatusText(ex.Message);
			}
		}

		private void InfoButton_Click(object sender, RoutedEventArgs e)
		{
			var tableColumnInfo = Manager.GetTableInfo(SelectTableName);
		}

		private void ColumnInfoButton_Click(object sender, RoutedEventArgs e)
		{
			var tableInfo = Manager.GetTablePrimaryKeyNames(SelectTableName);
		}

		private async void MergeButton_Click(object sender, RoutedEventArgs e)
		{
			var table = Manager.GetTableInfo(SelectTableName);
			var primaryKeyNames = Manager.GetTablePrimaryKeyNames(SelectTableName);
			var columnNames = table.Columns.Select(x => x.Name);
			var nonKeyColumnNames = columnNames.Except(primaryKeyNames);
			var columnNameSequence = string.Join(", ", columnNames);
			var columnNameAlphaSequence = string.Join(", ", columnNames.Select(x => $"@{x}"));
			var targetPrimaryKeyNames = string.Join(" and ", primaryKeyNames.Select(x => $"target.{x} = @{x}"));
			var updateSetNonKeyColumnNames = string.Join(", ", nonKeyColumnNames.Select(x => $"{x} = @{x}"));
			var builder = new StringBuilder();
			builder.AppendLine($"MERGE {SelectTableName} AS target");
			builder.AppendLine($"USING ( VALUES ( {columnNameAlphaSequence} ) ) AS source ( {columnNameSequence} )");
			builder.AppendLine($"ON ( {targetPrimaryKeyNames} )");
			builder.AppendLine($"WHEN MATCHED THEN");
			builder.AppendLine($"UPDATE SET");
			builder.AppendLine($"{updateSetNonKeyColumnNames}");
			builder.AppendLine($"WHEN NOT MATCHED THEN");
			builder.AppendLine($"INSERT ( {columnNameSequence} )");
			builder.AppendLine($"VALUES ( {columnNameAlphaSequence} );");

			await WebView.AppendEditorText($"{Environment.NewLine}{builder}");
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

		string GetTableNameFromQuery(string query)
		{
			string[] words = query.Split(separator, StringSplitOptions.RemoveEmptyEntries);
			int fromIndex = Array.FindIndex(words, word => word.Equals("from", StringComparison.OrdinalIgnoreCase));

			if (fromIndex != -1 && fromIndex < words.Length - 1)
			{
				return words[fromIndex + 1];
			}

			return string.Empty;
		}
	}
}
