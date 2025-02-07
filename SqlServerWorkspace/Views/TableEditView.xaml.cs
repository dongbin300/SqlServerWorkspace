using AvalonDock.Controls;

using SqlServerWorkspace.Data;
using SqlServerWorkspace.Extensions;

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

		public TableEditView()
		{
			InitializeComponent();
		}

		private void Window_Loaded(object sender, RoutedEventArgs e)
		{
			ColumnDataGrid.ItemsSource = new List<TableEditView_ColumnDataGrid>();
			if (TableName != string.Empty)
			{
				isModify = true;
				var result = Manager.GetTableInfo(TableName);
				TableNameTextBox.Text = result.Name;
				ColumnDataGrid.ItemsSource = result.Columns.Select(x => new TableEditView_ColumnDataGrid()
				{
					Name = x.Name,
					DataType = x.ToTypeString(),
					Description = x.Description,
					Key = x.IsKey,
					NotNull = x.IsNotNull,

					NamePrev = x.Name,
					DataTypePrev = x.ToTypeString(),
					DescriptionPrev = x.Description,
					KeyPrev = x.IsKey,
					NotNullPrev = x.IsNotNull
				}).ToList();
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
					foreach (var item in ColumnDataGrid.Items)
					{
						var row = (DataGridRow)ColumnDataGrid.ItemContainerGenerator.ContainerFromItem(item);
						if (row != null)
						{
							// 체크박스가 포함된 셀 찾기
							var cell = (DataGridCell)checkBoxColumn.GetCellContent(row).Parent;
							if (cell != null)
							{
								// 시각적 트리를 탐색하여 CheckBox 찾기
								var checkBox = cell.FindVisualChildren<CheckBox>().First();
								if (checkBox != null)
								{
									// CheckBox를 찾았을 때의 로직
									checkBox.Checked += (sender, e) =>
									{
										var checkBox = sender as CheckBox;
										if (checkBox?.DataContext is TableEditView_ColumnDataGrid dataContext)
										{
											dataContext.NotNull = true;
										}
									};
									checkBox.Unchecked += (sender, e) =>
									{
										var checkBox = sender as CheckBox;
										if (checkBox?.DataContext is TableEditView_ColumnDataGrid dataContext)
										{
											dataContext.NotNull = false;
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
			for (int i = 0; i < ColumnDataGrid.Items.Count; i++)
			{
				if (ColumnDataGrid.Items[i] is not TableEditView_ColumnDataGrid item)
				{
					continue;
				}

				if (item.Name == string.Empty)
				{
					continue;
				}

				columns.Add(new TableColumnInfo(item.Name, item.DataType, item.Key, item.NotNull, item.Description));
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

			var isNotNull = ColumnDataGrid.Columns[3].GetCellContent(button.FindParent<DataGridRow>()).Parent.FindVisualChildren<CheckBox>().First().IsChecked;

			if (button.DataContext is not TableEditView_ColumnDataGrid column)
			{
				return;
			}

			if (column.Name == string.Empty || column.DataType == string.Empty)
			{
				MessageBox.Show("No column name or data type");
				return;
			}

			/* Constraint */
			List<string> checkedKeyNames = [];
			for (int i = 0; i < ColumnDataGrid.Items.Count; i++)
			{
				var row = (DataGridRow)ColumnDataGrid.ItemContainerGenerator.ContainerFromIndex(i);
				if (row == null)
				{
					continue;
				}

				var cell = (DataGridCell)ColumnDataGrid.Columns[4].GetCellContent(row).Parent;
				if (cell == null)
				{
					continue;
				}

				var checkBox = cell.FindVisualChildren<CheckBox>().First();
				if (checkBox != null)
				{
					if (checkBox.IsChecked ?? false)
					{
						checkedKeyNames.Add(((TableEditView_ColumnDataGrid)ColumnDataGrid.Items[i]).NamePrev);
					}
				}
			}
			var primaryKeyNames = Manager.GetTablePrimaryKeyNames(TableName);

			string? result;
			if (column.NamePrev == string.Empty) // 새로운 컬럼 추가
			{
				result = Manager.AddColumn(TableName, column.Name, column.DataType, column.Description, isNotNull);
			}
			else // 기존 컬럼 변경
			{
				var dataType = column.DataType;
				var description = column.Description == column.DescriptionPrev ? null : column.Description;
				result = Manager.ModifyColumn(TableName, column.NamePrev, column.Name, dataType, description, isNotNull);
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

			if (button.DataContext is not TableEditView_ColumnDataGrid column)
			{
				return;
			}

			if (column.Name == string.Empty || column.DataType == string.Empty)
			{
				return;
			}

			if (MessageBox.Show("Are you sure you want to delete this?", "Confirm", MessageBoxButton.YesNoCancel) != MessageBoxResult.Yes)
			{
				return;
			}

			var result = Manager.DropColumn(TableName, column.Name);
			if (result != string.Empty)
			{
				MessageBox.Show(result);
				return;
			}

			var itemsSource = ColumnDataGrid.ItemsSource as List<TableEditView_ColumnDataGrid> ?? default!;
			itemsSource?.Remove(column);
			ColumnDataGrid.ItemsSource = null;
			ColumnDataGrid.ItemsSource = itemsSource;
		}

		string prev = string.Empty;
		string prevName = string.Empty;
		private void ColumnDataGrid_BeginningEdit(object sender, DataGridBeginningEditEventArgs e)
		{
			if (e.EditingEventArgs.OriginalSource is TextBlock textBlock)
			{
				var nameProperty = e.Row.DataContext.GetType().GetProperty("Name");
				if (nameProperty != null)
				{
					var nameValue = nameProperty.GetValue(e.Row.DataContext);
					prevName = nameValue?.ToString() ?? "";
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

				if (textBox.Text != prev)
				{
					var itemsSource = ColumnDataGrid.ItemsSource as List<TableEditView_ColumnDataGrid> ?? default!;

					switch (e.Column.Header.ToString())
					{
						case "Name":
							{
								var row = itemsSource.Find(x => x.Name.Equals(prev));
								if (row != null && row.NamePrev == string.Empty)
								{
									row.NamePrev = prev;
								}
							}
							break;

						case "Data Type":
							{
								var row = itemsSource.Find(x => x.Name.Equals(prevName));
								if (row != null && row.DataTypePrev == string.Empty)
								{
									row.DataTypePrev = prev;
								}
							}
							break;

						case "Description":
							{
								var row = itemsSource.Find(x => x.Name.Equals(prevName));
								if (row != null && row.DescriptionPrev == string.Empty)
								{
									row.DescriptionPrev = prev;
								}
							}
							break;

						default:
							break;
					}
				}
			}
		}
	}
}
