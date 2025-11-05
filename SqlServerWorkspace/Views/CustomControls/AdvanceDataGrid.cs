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
			else if (e.Key == Key.C && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control && (Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift)
			{
				CopyAllData();
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
			var contextMenu = new ContextMenu();

			// 선택된 데이터 복사 메뉴
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

			// 전체 데이터 복사 메뉴
			var copyAllMenuItem = new MenuItem { Header = "Copy All Data" };
			copyAllMenuItem.Click += (s, args) => CopyAllData();
			contextMenu.Items.Add(copyAllMenuItem);

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

				if (SelectedCells.Count == 0)
				{
					MessageBox.Show("복사할 데이터를 선택해주세요.");
					return;
				}

				// 가상화된 데이터 소스에서 직접 값을 가져오는 방식으로 개선
				if (ItemsSource is DataView dataView && dataView.Table != null)
				{
					var table = dataView.Table;
					var selectedCells = SelectedCells;

					if (selectedCells.Count == 1)
					{
						// 단일 셀 복사
						var cell = selectedCells[0];
						var value = GetCellValueFromDataItem(cell.Item, cell.Column);
						Clipboard.SetText(GetFormattedValue(value));
						return;
					}

					// 다중 셀 복사 - 데이터 소스에서 직접 가져오기
					var cellGroups = selectedCells
						.GroupBy(cell => cell.Item)
						.OrderBy(group => Items.IndexOf(group.Key))
						.ToList();

					if (!cellGroups.Any())
					{
						MessageBox.Show("선택된 데이터를 찾을 수 없습니다.");
						return;
					}

					var clipboardText = new StringBuilder();

					foreach (var group in cellGroups)
					{
						var sortedCells = group
							.OrderBy(cell => cell.Column.DisplayIndex)
							.ToList();

						if (sortedCells.Any())
						{
							var values = sortedCells.Select(cell =>
								GetCellValueFromDataItem(cell.Item, cell.Column));

							clipboardText.AppendLine(string.Join("\t", values));
						}
					}

					var text = clipboardText.ToString().TrimEnd('\r', '\n');
					if (!string.IsNullOrEmpty(text))
					{
						Clipboard.SetText(text);
					}
					else
					{
						MessageBox.Show("복사할 데이터를 처리할 수 없습니다.");
					}
				}
				else
				{
					// fallback: 기존 방식으로 시도
					FallbackCopyContent();
				}
			}
			catch (Exception ex)
			{
				MessageBox.Show($"복사 중 오류가 발생했습니다: {ex.Message}");
			}
		}

		/// <summary>
		/// 기존 방식으로 복사 (fallback)
		/// </summary>
		private void FallbackCopyContent()
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

		/// <summary>
		/// 데이터 아이템에서 셀 값을 가져오는 유연한 메서드 (SP 결과셋 지원)
		/// </summary>
		private static object GetCellValueFromDataItem(object dataItem, DataGridColumn column)
		{
			try
			{
				// DataRowView인 경우 (일반 테이블)
				if (dataItem is DataRowView rowView && column is DataGridBoundColumn boundColumn && boundColumn.Binding is Binding binding)
				{
					var columnName = binding.Path.Path;

					// 대괄호 제거 처리 (SP 결과셋의 경우)
					if (columnName.StartsWith("[") && columnName.EndsWith("]"))
					{
						columnName = columnName.Substring(1, columnName.Length - 2);
					}

					// 컬럼 존재 여부 확인 후 값 가져오기
					if (rowView.Row.Table.Columns.Contains(columnName))
					{
						return rowView[columnName];
					}

					// 대괄호 제거한 이름으로도 없으면 원래 이름으로 시도
					if (!columnName.StartsWith("[") && !columnName.EndsWith("]"))
					{
						var bracketedName = $"[{columnName}]";
						if (rowView.Row.Table.Columns.Contains(bracketedName))
						{
							return rowView[bracketedName];
						}
					}
				}

				// UI 요소에서 값 가져오기 (fallback)
				return GetCellValueFromUI(dataItem, column);
			}
			catch
			{
				// 모든 방법이 실패하면 UI에서 시도
				return GetCellValueFromUI(dataItem, column);
			}
		}

		/// <summary>
		/// UI 요소에서 셀 값 가져오기 (fallback 방식)
		/// </summary>
		private static object GetCellValueFromUI(object dataItem, DataGridColumn column)
		{
			if (column.GetCellContent(dataItem) is TextBlock textBlock)
				return textBlock.Text;

			// FrameworkElement에서 직접 값 찾기 시도
			if (column.GetCellContent(dataItem) is FrameworkElement element)
			{
				// 다양한 UI 요소 타입 처리
				return element switch
				{
					TextBlock tb => tb.Text,
					TextBox textBox => textBox.Text,
					Label label => label.Content?.ToString() ?? "",
					ContentControl contentControl => contentControl.Content?.ToString() ?? "",
					_ => ""
				};
			}

			return "";
		}

		/// <summary>
		/// 데이터 형식에 맞게 값 포맷팅
		/// </summary>
		private static string GetFormattedValue(object value)
		{
			if (value == DBNull.Value || value == null)
				return "";

			if (value is DateTime dateTime)
				return dateTime.ToString("yyyy-MM-dd HH:mm:ss");

			return value.ToString();
		}


		/// <summary>
		/// 전체 데이터를 복사하는 기능 (Ctrl+Shift+C)
		/// UI에 표시된 데이터가 아닌 전체 데이터 소스를 기준으로 복사
		/// 대용량 데이터를 처리할 수 있도록 최적화
		/// </summary>
		private void CopyAllData()
		{
			try
			{
				if (ItemsSource is not DataView dataView || dataView.Table == null)
				{
					MessageBox.Show("데이터를 찾을 수 없습니다.");
					return;
				}

				var table = dataView.Table;
				var rowCount = table.Rows.Count;
				var colCount = table.Columns.Count;

				// 대용량 데이터 경고
				if (rowCount > 10000)
				{
					var result = MessageBox.Show(
						$"데이터가 {rowCount:N0}개 행으로 매우 많습니다.\n복사하는데 시간이 걸릴 수 있습니다.\n계속하시겠습니까?",
						"대용량 데이터 복사",
						MessageBoxButton.YesNo,
						MessageBoxImage.Warning);

					if (result != MessageBoxResult.Yes)
						return;
				}

				var clipboardText = new StringBuilder();

				// 예상 크기 계산 (메모리 최적화)
				var estimatedSize = rowCount * colCount * 50; // 평균 50자 예상
				if (estimatedSize > 50 * 1024 * 1024) // 50MB 초과 시
				{
					MessageBox.Show("데이터가 너무 많아 클립보드에 복사할 수 없습니다.\n데이터를 필터링하거나 여러 번에 나누어 복사해주세요.",
						"복사 제한", MessageBoxButton.OK, MessageBoxImage.Warning);
					return;
				}

				// 헤더 추가
				var headers = table.Columns.Cast<DataColumn>().Select(column => column.ColumnName);
				clipboardText.AppendLine(string.Join("\t", headers));

				// 데이터 추가 (StringBuilder 최적화)
				foreach (DataRow row in table.Rows)
				{
					var values = new string[colCount];
					for (int i = 0; i < colCount; i++)
					{
						var value = row[i];
						values[i] = value == DBNull.Value ? "" :
							value is DateTime dateTime ? dateTime.ToString("yyyy-MM-dd HH:mm:ss") :
							value.ToString();
					}
					clipboardText.AppendLine(string.Join("\t", values));
				}

				var text = clipboardText.ToString().TrimEnd('\r', '\n');
				Clipboard.SetText(text);

				//MessageBox.Show($"{rowCount:N0}개 행이 복사되었습니다.", "복사 완료", MessageBoxButton.OK, MessageBoxImage.Information);
			}
			catch (OutOfMemoryException)
			{
				MessageBox.Show("메모리가 부족합니다.\n데이터를 필터링하거나 일부만 선택해서 복사해주세요.",
					"메모리 부족", MessageBoxButton.OK, MessageBoxImage.Error);
			}
			catch (Exception ex)
			{
				MessageBox.Show($"복사 중 오류가 발생했습니다: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
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
