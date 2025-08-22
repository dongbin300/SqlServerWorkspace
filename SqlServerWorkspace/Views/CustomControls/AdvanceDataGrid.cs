using System.Data;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;

namespace SqlServerWorkspace.Views.CustomControls
{
	public class AdvanceDataGrid : DataGrid
	{
		public AdvanceDataGrid()
		{
			MouseRightButtonUp += DataGrid_MouseRightButtonUp;
			PreviewKeyDown += DataGrid_PreviewKeyDown;
			LoadingRow += DataGrid_LoadingRow;
			AddingNewItem += DataGrid_AddingNewItem;
		}

		private void DataGrid_AddingNewItem(object? sender, AddingNewItemEventArgs e)
		{
			if (ItemsSource is DataView dataView)
			{
				var table = dataView.Table ?? default!;
				var newRow = table.NewRow();

				foreach (DataColumn column in table.Columns)
				{
					column.AllowDBNull = true;
					newRow[column] = DBNull.Value;
				}

				table.Rows.Add(newRow);
			}
		}

		private void DataGrid_LoadingRow(object? sender, DataGridRowEventArgs e)
		{
			e.Row.Header = (e.Row.GetIndex() + 1).ToString();
		}

		private void DataGrid_PreviewKeyDown(object sender, KeyEventArgs e)
		{
			if (e.Key == Key.C && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
			{
				CopyContent();
				e.Handled = true;
			}
			else if (e.Key == Key.V && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
			{
				PasteClipboardContent();
				e.Handled = true;
			}
			else if (e.Key == Key.Delete)
			{
				DeleteSelectedCells();
				e.Handled = true;
			}
		}

		private void DataGrid_MouseRightButtonUp(object sender, MouseButtonEventArgs e)
		{
			if (SelectedItem == null)
			{
				return;
			}

			var contextMenu = new ContextMenu();

			var copyMenuItem = new MenuItem { Header = "Copy" };
			copyMenuItem.Click += (s, args) =>
			{
				if (ItemsSource is not DataView || SelectedItem == null)
				{
					return;
				}

				if (SelectedItem is not DataRowView dataRowView)
				{
					return;
				}

				var selectedRow = dataRowView.Row;
				var text = string.Join('\t', selectedRow.ItemArray);
				Clipboard.SetText(text);
			};
			contextMenu.Items.Add(copyMenuItem);

			var pasteMenuItem = new MenuItem { Header = "Paste" };
			pasteMenuItem.Click += (s, args) =>
			{
				if (ItemsSource is not DataView || SelectedItem == null)
				{
					return;
				}

				if (SelectedItem is not DataRowView dataRowView)
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
			};
			if (string.IsNullOrEmpty(Clipboard.GetText()))
			{
				pasteMenuItem.IsEnabled = false;
			}
			contextMenu.Items.Add(pasteMenuItem);

			var duplicateMenuItem = new MenuItem { Header = "Duplicate" };
			duplicateMenuItem.Click += (s, args) =>
			{
				if (ItemsSource is not DataView dataView || SelectedItem == null)
				{
					return;
				}

				if (SelectedItem is not DataRowView dataRowView)
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

				int selectedIndex = SelectedIndex;
				dataView.Table.Rows.InsertAt(newRow, selectedIndex + 1);
			};
			contextMenu.Items.Add(duplicateMenuItem);

			var deleteMenuItem = new MenuItem { Header = "Delete" };
			deleteMenuItem.Click += (s, args) =>
			{
				if (SelectedItem is DataRowView dataRowView)
				{
					dataRowView.Row.Delete();
				}
			};
			contextMenu.Items.Add(deleteMenuItem);

			contextMenu.IsOpen = true;
		}

		private void CopyContent()
		{
			try
			{
				if (Items.Count == 0) return;

				if (SelectedCells.Count == 1)
				{
					var cell = SelectedCells[0];
					var value = GetCellValue(cell.Column, cell.Item);
					Clipboard.SetText(value?.ToString() ?? "");
				}
				else
				{
					var groupedCells = SelectedCells
						.GroupBy(cell => cell.Item)
						.Select(group => group.OrderBy(cell => cell.Column.DisplayIndex)
						.Select(cell => GetCellValue(cell.Column, cell.Item)))
						.ToList();

					var clipboardText = new StringBuilder();
					foreach (var row in groupedCells)
					{
						clipboardText.AppendLine(string.Join("\t", row));
					}

					Clipboard.SetText(clipboardText.ToString().TrimEnd('\r', '\n'));
				}
			}
			catch (Exception ex)
			{
				MessageBox.Show(ex.Message);
			}
		}


		private static string GetCellValue(DataGridColumn column, object row)
		{
			if (column.GetCellContent(row) is TextBlock textBlock)
				return textBlock.Text;
			return "";
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
				if (SelectedCells.Count == 0)
					return;

				var selectedCell = SelectedCells.First();
				int startRowIndex = Items.IndexOf(selectedCell.Item);
				int startColIndex = selectedCell.Column.DisplayIndex;
				if (ItemsSource is not DataView dataView)
					return;

				var table = dataView.Table;
				if (table == null)
					return;

				int requiredRowCount = startRowIndex + lines.Length;
				while (dataView.Count < requiredRowCount)
				{
					var newRow = table.NewRow();
					table.Rows.Add(newRow);
				}

				Items.Refresh();

				for (int i = 0; i < lines.Length; i++)
				{
					int rowIndex = startRowIndex + i;
					var row = dataView[rowIndex];
					var values = lines[i].Split('\t');

					for (int j = 0; j < values.Length; j++)
					{
						int colIndex = startColIndex + j;
						if (colIndex >= Columns.Count)
							break;

						var column = Columns[colIndex];
						if (column is DataGridBoundColumn boundColumn &&
							boundColumn.Binding is Binding binding)
						{
							string columnName = binding.Path.Path;
							if (table.Columns.Contains(columnName))
							{
								var columnType = table.Columns[columnName]?.DataType;
								try
								{
									var convertedValue = Convert.ChangeType(values[j], columnType!);
									row[columnName] = convertedValue ?? DBNull.Value;
								}
								catch
								{
									row[columnName] = values[j];
								}
							}
						}
					}
				}
			}
			catch (Exception ex)
			{
				MessageBox.Show(ex.Message);
			}
		}

		private void DeleteSelectedCells()
		{
			try
			{
				if (SelectedCells.Count == 0)
					return;

				foreach (var selectedCell in SelectedCells)
				{
					if (selectedCell.Item is DataRowView rowView &&
						selectedCell.Column is DataGridBoundColumn boundColumn &&
						boundColumn.Binding is Binding binding)
					{
						string columnName = binding.Path.Path;
						if (rowView.Row.Table.Columns.Contains(columnName))
						{
							rowView[columnName] = DBNull.Value;
						}
					}
				}
			}
			catch (Exception ex)
			{
				MessageBox.Show(ex.Message);
			}
		}
	}
}
