using Microsoft.Web.WebView2.Core;

using SqlServerWorkspace.Data;
using SqlServerWorkspace.Extensions;

using System.Data;
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
				var tableInfo = Manager.GetTableInfo(SelectTableName);

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
						Header = new string[] { column.ColumnName, tableInfo.Columns.First(x => x.Name.Equals(column.ColumnName)).ToTypeString() },
						Binding = binding
					};

					//var cellStyle = new Style(typeof(DataGridCell));
					//cellStyle.Setters.Add(new Setter(DataGridCell.BackgroundProperty, new SolidColorBrush(Color.FromRgb(63, 63, 63))));
					//cellStyle.Setters.Add(new Setter(DataGridCell.ForegroundProperty, Brushes.White));
					//cellStyle.Setters.Add(new Setter(DataGridCell.BorderBrushProperty, Brushes.Transparent));
					//cellStyle.Setters.Add(new Setter(DataGridCell.BorderThicknessProperty, new Thickness(0)));

					//cellStyle.Triggers.Add(new DataTrigger
					//{
					//	Binding = new Binding("IsSelected") { RelativeSource = new RelativeSource(RelativeSourceMode.Self) },
					//	Value = true,
					//	Setters =
					//	{
					//		new Setter(DataGridCell.BackgroundProperty, new SolidColorBrush(Color.FromRgb(49, 49, 49))),
					//		new Setter(DataGridCell.ForegroundProperty, Brushes.White)
					//	}
					//});

					//dataGrid.CellStyle = cellStyle;

					//var templateColumn = new DataGridTemplateColumn
					//{
					//	Header = column.ColumnName
					//};

					//var cellTemplate = new DataTemplate();
					//var textBlockFactory = new FrameworkElementFactory(typeof(TextBlock));
					//textBlockFactory.SetBinding(TextBlock.TextProperty, new Binding(column.ColumnName));
					//textBlockFactory.SetValue(TextBlock.BackgroundProperty, new SolidColorBrush(Colors.Transparent));
					//textBlockFactory.SetValue(TextBlock.ForegroundProperty, Brushes.White);
					//cellTemplate.VisualTree = textBlockFactory;

					//var editingTemplate = new DataTemplate();
					//var textBoxFactory = new FrameworkElementFactory(typeof(TextBox));
					//textBoxFactory.SetBinding(TextBox.TextProperty, new Binding(column.ColumnName) { Mode = BindingMode.TwoWay });
					//textBoxFactory.SetValue(BackgroundProperty, new SolidColorBrush(Colors.Transparent));
					//textBoxFactory.SetValue(ForegroundProperty, Brushes.White);
					//textBoxFactory.SetValue(BorderBrushProperty, Brushes.Transparent);
					//textBoxFactory.SetValue(BorderThicknessProperty, new Thickness(0));
					//textBoxFactory.SetValue(HorizontalContentAlignmentProperty, HorizontalAlignment.Center);
					//textBoxFactory.SetValue(VerticalAlignmentProperty, VerticalAlignment.Center);
					//editingTemplate.VisualTree = textBoxFactory;

					//templateColumn.CellTemplate = cellTemplate;
					//templateColumn.CellEditingTemplate = editingTemplate;

					//TableDataGrid.Columns.Add(templateColumn);
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

				Common.AppendLogDetail($"{query}");
			}
			catch (Exception ex)
			{
				Common.AppendLogDetail(ex.Message);
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

				var result = Manager.TableTransaction(SelectTableName, SelectTable, currentTable);
				Common.AppendLogDetail(result);
			}
			catch (Exception ex)
			{
				Common.AppendLogDetail(ex.Message);
			}
		}

		private async void MergeButton_Click(object sender, RoutedEventArgs e)
		{
			var mergeQuery = Manager.GetMergeQuery(SelectTableName);

			await WebView.AppendEditorText($"{Environment.NewLine}{mergeQuery}");

			Common.AppendLogDetail($"{SelectTableName} MERGE");
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
