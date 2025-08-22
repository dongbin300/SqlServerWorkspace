using AvalonDock.Controls;

using SqlServerWorkspace.Data;
using SqlServerWorkspace.Extensions;

using System.Data;
using System.Windows;
using System.Windows.Controls;

namespace SqlServerWorkspace.Views
{
	public class TableEditView_ColumnDataGrid
	{
		public string Name { get; set; } = string.Empty;
		public string DataType { get; set; } = string.Empty;
		public string Description { get; set; } = string.Empty;
		public bool Key { get; set; }
		public bool NotNull { get; set; }

		public string NamePrev { get; set; } = string.Empty;
		public string DataTypePrev { get; set; } = string.Empty;
		public string DescriptionPrev { get; set; } = string.Empty;
		public bool KeyPrev { get; set; }
		public bool NotNullPrev { get; set; }
	}

	/// <summary>
	/// NewTableView.xaml에 대한 상호 작용 논리
	/// </summary>
	public partial class TableEditView : Window
	{
		public SqlManager Manager = default!;
		public string TableName = string.Empty;
		private bool isModify = false;
		private DataTable columnDataTable = new DataTable();

		public TableEditView()
		{
			InitializeComponent();
			InitializeDataTable();
		}

		private void InitializeDataTable()
		{
			columnDataTable.Columns.Add("Name", typeof(string));
			columnDataTable.Columns.Add("DataType", typeof(string));
			columnDataTable.Columns.Add("Description", typeof(string));
			columnDataTable.Columns.Add("Key", typeof(bool));
			columnDataTable.Columns.Add("NotNull", typeof(bool));
			columnDataTable.Columns.Add("NamePrev", typeof(string));
			columnDataTable.Columns.Add("DataTypePrev", typeof(string));
			columnDataTable.Columns.Add("DescriptionPrev", typeof(string));
			columnDataTable.Columns.Add("KeyPrev", typeof(bool));
			columnDataTable.Columns.Add("NotNullPrev", typeof(bool));
		}

		private void Window_Loaded(object sender, RoutedEventArgs e)
		{
			ColumnDataGrid.ItemsSource = columnDataTable.DefaultView;

			if (TableName != string.Empty)
			{
				isModify = true;
				var result = Manager.GetTableInfo(TableName);
				TableNameTextBox.Text = result.Name;

				foreach (var column in result.Columns)
				{
					var row = columnDataTable.NewRow();
					row["Name"] = column.Name;
					row["DataType"] = column.ToTypeString();
					row["Description"] = column.Description;
					row["Key"] = column.IsKey;
					row["NotNull"] = column.IsNotNull;
					row["NamePrev"] = column.Name;
					row["DataTypePrev"] = column.ToTypeString();
					row["DescriptionPrev"] = column.Description;
					row["KeyPrev"] = column.IsKey;
					row["NotNullPrev"] = column.IsNotNull;

					columnDataTable.Rows.Add(row);
				}
			}

			if (isModify)
			{
				Title = "Modify Table";
				ModifyButton.Visibility = Visibility.Collapsed;
				CancelButton.Visibility = Visibility.Collapsed;
				MakeButton.Visibility = Visibility.Collapsed;
				TextButton.Visibility = Visibility.Collapsed;
			}
			else
			{
				Title = "Make Table";
				ModifyButton.Visibility = Visibility.Collapsed;
				CancelButton.Visibility = Visibility.Collapsed;
				SaveColumn.Visibility = Visibility.Collapsed;
				DeleteColumn.Visibility = Visibility.Collapsed;
			}

			foreach (var column in ColumnDataGrid.Columns)
			{
				if (column is DataGridCheckBoxColumn checkBoxColumn)
				{
					foreach (DataRowView rowView in columnDataTable.DefaultView)
					{
						var row = (DataGridRow)ColumnDataGrid.ItemContainerGenerator.ContainerFromItem(rowView);
						if (row != null)
						{
							var cell = (DataGridCell)checkBoxColumn.GetCellContent(row).Parent;
							if (cell != null)
							{
								var checkBox = cell.FindVisualChildren<CheckBox>().First();
								if (checkBox != null)
								{
									checkBox.Checked += (sender, e) =>
									{
										var checkBox = sender as CheckBox;
										if (checkBox?.DataContext is DataRowView dataRowView)
										{
											dataRowView["NotNull"] = true;
										}
									};
									checkBox.Unchecked += (sender, e) =>
									{
										var checkBox = sender as CheckBox;
										if (checkBox?.DataContext is DataRowView dataRowView)
										{
											dataRowView["NotNull"] = false;
										}
									};
								}
							}
						}
					}
				}
			}
		}

		private List<TableColumnInfo> MakeTableColumnInfo()
		{
			var columns = new List<TableColumnInfo>();

			foreach (DataRowView rowView in columnDataTable.DefaultView)
			{
				var name = rowView["Name"]?.ToString() ?? string.Empty;
				if (name == string.Empty)
				{
					continue;
				}

				var dataType = rowView["DataType"]?.ToString() ?? string.Empty;
				var description = rowView["Description"]?.ToString() ?? string.Empty;
				var key = Convert.ToBoolean(rowView["Key"]);
				var notNull = Convert.ToBoolean(rowView["NotNull"]);

				columns.Add(new TableColumnInfo(name, dataType, key, notNull, description));
			}

			return columns;
		}

		private void TextButton_Click(object sender, RoutedEventArgs e)
		{
			Clipboard.SetText(Manager.GetNewTableQuery(TableNameTextBox.Text, MakeTableColumnInfo()));
		}

		private void MakeButton_Click(object sender, RoutedEventArgs e)
		{
			var result = Manager.MakeTable(TableNameTextBox.Text, MakeTableColumnInfo());
			if (result != string.Empty)
			{
				MessageBox.Show(result);
				return;
			}

			DialogResult = true;
			Close();
		}

		private void ModifyButton_Click(object sender, RoutedEventArgs e)
		{
			var result = Manager.Rename(TableName, TableNameTextBox.Text); // 테이블명 변경
			if (result != string.Empty)
			{
				MessageBox.Show(result);
				return;
			}

			DialogResult = true;
			Close();
		}

		private void CancelButton_Click(object sender, RoutedEventArgs e)
		{
			DialogResult = false;
			Close();
		}

		private void ColumnDataGrid_PreviewMouseWheel(object sender, System.Windows.Input.MouseWheelEventArgs e)
		{
			var scrollViewer = ColumnDataGrid.FindParent<ScrollViewer>();
			if (scrollViewer != null)
			{
				scrollViewer.ScrollToVerticalOffset(scrollViewer.VerticalOffset - e.Delta / 3);
				e.Handled = true;
			}
		}

		private void ColumnSaveButton_Click(object sender, RoutedEventArgs e)
		{
			if (sender is not Button button)
			{
				return;
			}

			var row = button.FindParent<DataGridRow>();
			if (row?.DataContext is not DataRowView rowView)
			{
				return;
			}

			var isNotNull = ColumnDataGrid.Columns[3].GetCellContent(row).Parent.FindVisualChildren<CheckBox>().First().IsChecked;

			var name = rowView["Name"]?.ToString() ?? string.Empty;
			var dataType = rowView["DataType"]?.ToString() ?? string.Empty;

			if (name == string.Empty || dataType == string.Empty)
			{
				MessageBox.Show("No column name or data type");
				return;
			}

			/* Constraint */
			List<string> checkedKeyNames = [];
			foreach (DataRowView item in columnDataTable.DefaultView)
			{
				var itemRow = (DataGridRow)ColumnDataGrid.ItemContainerGenerator.ContainerFromItem(item);
				if (itemRow == null)
				{
					continue;
				}

				var cell = (DataGridCell)ColumnDataGrid.Columns[4].GetCellContent(itemRow).Parent;
				if (cell == null)
				{
					continue;
				}

				var checkBox = cell.FindVisualChildren<CheckBox>().First();
				if (checkBox != null)
				{
					if (checkBox.IsChecked ?? false)
					{
						checkedKeyNames.Add(item["NamePrev"]?.ToString() ?? string.Empty);
					}
				}
			}
			var primaryKeyNames = Manager.GetTablePrimaryKeyNames(TableName);

			string? result;
			var namePrev = rowView["NamePrev"]?.ToString() ?? string.Empty;
			var description = rowView["Description"]?.ToString() ?? string.Empty;
			var descriptionPrev = rowView["DescriptionPrev"]?.ToString() ?? string.Empty;

			if (namePrev == string.Empty) // 새로운 컬럼 추가
			{
				result = Manager.AddColumn(TableName, name, dataType, description, isNotNull);
			}
			else // 기존 컬럼 변경
			{
				var descriptionToUpdate = description == descriptionPrev ? null : description;
				result = Manager.ModifyColumn(TableName, namePrev, name, dataType, descriptionToUpdate, isNotNull);
			}

			if (!checkedKeyNames.SequenceEqual(primaryKeyNames)) // 기본 키 변경
			{
				result = Manager.ModifyConstraint(TableName, checkedKeyNames);
			}

			if (result != string.Empty)
			{
				MessageBox.Show(result);
				return;
			}
		}

		private void ColumnDeleteButton_Click(object sender, RoutedEventArgs e)
		{
			if (sender is not Button button)
			{
				return;
			}

			var row = button.FindParent<DataGridRow>();
			if (row?.DataContext is not DataRowView rowView)
			{
				return;
			}

			var name = rowView["Name"]?.ToString() ?? string.Empty;
			var dataType = rowView["DataType"]?.ToString() ?? string.Empty;

			if (name == string.Empty || dataType == string.Empty)
			{
				return;
			}

			if (MessageBox.Show("Are you sure you want to delete this?", "Confirm", MessageBoxButton.YesNoCancel) != MessageBoxResult.Yes)
			{
				return;
			}

			var result = Manager.DropColumn(TableName, name);
			if (result != string.Empty)
			{
				MessageBox.Show(result);
				return;
			}

			// DataTable에서 행 제거
			rowView.Delete();
		}

		string prev = string.Empty;
		string prevName = string.Empty;
		private void ColumnDataGrid_BeginningEdit(object sender, DataGridBeginningEditEventArgs e)
		{
			if (e.EditingEventArgs.OriginalSource is TextBlock textBlock)
			{
				if (e.Row.DataContext is DataRowView rowView)
				{
					prevName = rowView["Name"]?.ToString() ?? string.Empty;
				}

				prev = textBlock.Text;
			}
			else if (e.EditingEventArgs.OriginalSource is Grid)
			{
				prev = string.Empty;
			}
			else
			{
				return;
			}
		}

		private void ColumnDataGrid_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
		{
			if (e.EditAction == DataGridEditAction.Commit)
			{
				if (e.EditingElement is not TextBox textBox)
				{
					return;
				}

				if (textBox.Text != prev && e.Row.DataContext is DataRowView rowView)
				{
					switch (e.Column.Header.ToString())
					{
						case "Name":
							{
								if (rowView["NamePrev"]?.ToString() == string.Empty)
								{
									rowView["NamePrev"] = prev;
								}
							}
							break;

						case "Data Type":
							{
								if (rowView["DataTypePrev"]?.ToString() == string.Empty)
								{
									rowView["DataTypePrev"] = prev;
								}
							}
							break;

						case "Description":
							{
								if (rowView["DescriptionPrev"]?.ToString() == string.Empty)
								{
									rowView["DescriptionPrev"] = prev;
								}
							}
							break;

						default:
							break;
					}
				}
			}

			QueryTextBox.Text = Manager.GetNewTableQuery(TableNameTextBox.Text, MakeTableColumnInfo());
		}
	}
}