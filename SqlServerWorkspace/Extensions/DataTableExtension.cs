using SqlServerWorkspace.Data;

using System.Data;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;

namespace SqlServerWorkspace.Extensions
{
	public static class DataTableExtension
	{
		public static IEnumerable<string> Field(this DataTable dataTable, string fieldName)
		{
			return dataTable.Rows.Cast<DataRow>().Where(row => row[fieldName] != DBNull.Value).Select(row => row[fieldName].ToString() ?? string.Empty);
		}

		public static DataTable ToDataTable(this DataGrid dataGrid)
		{
			var dataTable = new DataTable();

			foreach (var column in dataGrid.Columns)
			{
				if (column is DataGridColumn dataGridColumn)
				{
					var columnName = ((string[])dataGridColumn.Header)[0];
					dataTable.Columns.Add(columnName, typeof(string));
				}
			}

			foreach (var item in dataGrid.Items)
			{
				if (item is not DataRowView row)
				{
					continue;
				}

				DataRow newRow = dataTable.NewRow();
				foreach (var column in dataGrid.Columns)
				{
					if (column is DataGridColumn dataGridColumn)
					{
						var columnName = ((string[])dataGridColumn.Header)[0];
						if (string.IsNullOrEmpty(columnName))
						{
							continue;
						}

						var value = row[columnName];

						if (value is DateTime dateTime)
						{
							value = dateTime.ToString("yyyy-MM-dd HH:mm:ss");
						}

						newRow[columnName] = value ?? DBNull.Value;
					}
				}
				dataTable.Rows.Add(newRow);
			}
			return dataTable;
		}

		public static void FillSqlDataTable(this DataGrid dataGrid, DataTable dataTable, TableInfo tableInfo)
		{
			foreach (DataColumn column in dataTable.Columns)
			{
				var binding = new Binding(column.ColumnName)
				{
					//Converter = (IValueConverter)Application.Current.FindResource("DBNullToNullStringConverter")
				};

				if (column.DataType == typeof(DateTime))
				{
					binding.StringFormat = "yyyy-MM-dd HH:mm:ss";
				}

				var tableColumn = tableInfo.Columns.First(x => x.Name.Equals(column.ColumnName));
				var dataGridColumn = new DataGridTextColumn
				{
					Header = new string[]
					{
						column.ColumnName,				// Column Name
						tableColumn.ToTypeString(),		// Column DataType
						tableColumn.IsKey ? "*" : "",	// Column Key Flag
						tableColumn.Description,		// Column Description
					},
					Binding = binding,
					EditingElementStyle = (Style)Application.Current.Resources["DataGridCellTextBox"]
				};

				//var textBoxStyle = new Style(typeof(TextBox));
				//textBoxStyle.Setters.Add(new Setter(Control.ForegroundProperty, Application.Current.Resources["DarkForeground"]));
				//textBoxStyle.Setters.Add(new Setter(Control.BackgroundProperty, Brushes.Transparent));
				//textBoxStyle.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(0)));
				//textBoxStyle.Setters.Add(new Setter(System.Windows.Controls.Primitives.TextBoxBase.CaretBrushProperty, Application.Current.Resources["DarkForeground"]));

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
				dataGrid.Columns.Add(dataGridColumn);
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
		}

		public static void FillSqlDataTableSimple(this DataGrid dataGrid, DataTable dataTable)
		{
			foreach (DataColumn column in dataTable.Columns)
			{
				var binding = new Binding(column.ColumnName)
				{
					Path = new PropertyPath($"[{column.ColumnName}]")
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

				dataGrid.Columns.Add(dataGridColumn);
			}
		}
	}
}
