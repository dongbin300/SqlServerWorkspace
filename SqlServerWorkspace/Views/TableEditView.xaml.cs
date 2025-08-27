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

			// 체크박스 이벤트 처리는 데이터바인딩으로 자동 처리됨
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
				var _key = rowView["Key"]?.ToString() ?? string.Empty;
				var key = _key == "True";
				var _notNull = rowView["NotNull"]?.ToString() ?? string.Empty;
				var notNull = _notNull == "True";

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
				if (e.Column.Header.ToString() == "Key" || e.Column.Header.ToString() == "NotNull")
				{
					if (e.EditingElement is CheckBox checkBox && e.Row.DataContext is DataRowView rowView)
					{
						if (e.Column.Header.ToString() == "Key")
							rowView["Key"] = checkBox.IsChecked ?? false;
						else if (e.Column.Header.ToString() == "NotNull")
							rowView["NotNull"] = checkBox.IsChecked ?? false;
					}
					Dispatcher.BeginInvoke(new Action(() =>
					{
						if (Manager != null && !string.IsNullOrEmpty(TableNameTextBox?.Text))
						{
							var columns = MakeTableColumnInfo();
							QueryTextBox.Text = Manager.GetNewTableQuery(TableNameTextBox.Text, columns);
						}
					}), System.Windows.Threading.DispatcherPriority.Background);
				}
				else if (e.EditingElement is TextBox textBox && e.Row.DataContext is DataRowView rowView)
				{
					var columnName = e.Column.Header.ToString();

					switch (columnName)
					{
						case "Name":
							if (string.IsNullOrEmpty(rowView["NamePrev"]?.ToString()))
								rowView["NamePrev"] = prev;
							break;
						case "Data Type":
							if (string.IsNullOrEmpty(rowView["DataTypePrev"]?.ToString()))
								rowView["DataTypePrev"] = prev;
							break;
						case "Description":
							if (string.IsNullOrEmpty(rowView["DescriptionPrev"]?.ToString()))
								rowView["DescriptionPrev"] = prev;
							break;
					}
					Dispatcher.BeginInvoke(new Action(() =>
					{
						if (Manager != null && !string.IsNullOrEmpty(TableNameTextBox?.Text))
						{
							var columns = MakeTableColumnInfo();
							QueryTextBox.Text = Manager.GetNewTableQuery(TableNameTextBox.Text, columns);
						}
					}), System.Windows.Threading.DispatcherPriority.Background);
				}
			}
		}

		private void TableNameTextBox_TextChanged(object sender, TextChangedEventArgs e)
		{
			UpdateQuery();
		}

		private void ColumnDataGrid_CurrentCellChanged(object sender, EventArgs e)
		{
			UpdateQuery();
		}

		private void UpdateQuery()
		{
			if (Manager == null || string.IsNullOrEmpty(TableNameTextBox?.Text))
			{
				return;
			}

			Dispatcher.BeginInvoke(new Action(() =>
			{
				try
				{
					var columns = MakeTableColumnInfo();
					if (columns.Count > 0 || !isModify)
					{
						QueryTextBox.Text = Manager.GetNewTableQuery(TableNameTextBox.Text, columns);
					}
				}
				catch (Exception ex)
				{
					System.Diagnostics.Debug.WriteLine($"Query generation error: {ex.Message}");
					QueryTextBox.Text = ""; // 오류 시 빈 문자열
				}
			}), System.Windows.Threading.DispatcherPriority.Background);
		}
	}
}