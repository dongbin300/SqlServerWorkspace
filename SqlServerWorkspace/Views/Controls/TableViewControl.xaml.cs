using Microsoft.Web.WebView2.Core;

using SqlServerWorkspace.Data;
using SqlServerWorkspace.Enums;
using SqlServerWorkspace.Extensions;

using System.Data;
using System.Text;
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
		public class WhereCondition
		{
			public string LogicalOperator { get; set; } = string.Empty;
			public string Column { get; set; } = string.Empty;
			public string Operator { get; set; } = string.Empty;
			public string Value { get; set; } = string.Empty;
		}

		public class OrderCondition
		{
			public string Column { get; set; } = string.Empty;
			public string Direction { get; set; } = string.Empty;
		}

		private int wherePanelCounter = 1;
		private int orderPanelCounter = 1;
		private List<StackPanel> wherePanels = [];
		private List<StackPanel> orderPanels = [];

		public SqlManager Manager { get; set; } = default!;
		public string Header { get; set; } = string.Empty;
		public DataTable SelectTable = default!;
		public string SelectTableName = string.Empty;
		private TableInfo _tableInfo = default!;

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
				_tableInfo = tableInfo;

				TableDataGrid.FillSqlDataTable(table, tableInfo);

				// 데이터그리드에 행 수 정보 업데이트
				UpdateDataRowCount(table2.Rows.Count);

				Common.Log($"{query}", LogType.Info);
			}
			catch (Exception ex)
			{
				Common.Log(ex.Message, LogType.Error);
			}
		}

		/// <summary>
		/// 데이터그리드에 로딩된 행 수를 업데이트
		/// </summary>
		private void UpdateDataRowCount(int rowCount)
		{
			RowCountText.Text = $"총 {rowCount:N0}건";
		}

		private async void RunButton_Click(object sender, RoutedEventArgs e)
		{
			string query;

			if (WhereCheckBox.IsChecked == true && OrderCheckBox.IsChecked == true)
			{
				// Where와 OrderBy 모두 사용
				query = GetQueryStringFromFilter();
			}
			else if (WhereCheckBox.IsChecked == true)
			{
				// Where만 사용
				query = GetWhereQueryString();
			}
			else if (OrderCheckBox.IsChecked == true)
			{
				// OrderBy만 사용
				query = GetOrderQueryString();
			}
			else
			{
				// 필터 없이 에디터에서 쿼리 사용
				query = await WebView.GetEditorText();
			}

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

		private void CopyStructureButton_Click(object sender, RoutedEventArgs e)
		{
			try
			{
				if (_tableInfo == null || string.IsNullOrEmpty(SelectTableName))
				{
					MessageBox.Show("테이블 정보를 찾을 수 없습니다.", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
					return;
				}

				var structure = GenerateTableStructure();
				Clipboard.SetText(structure);

				//MessageBox.Show($"{SelectTableName} 테이블 구조가 클립보드에 복사되었습니다.", "복사 완료", MessageBoxButton.OK, MessageBoxImage.Information);
				Common.Log($"{SelectTableName} TABLE STRUCTURE COPIED", LogType.Info);
			}
			catch (Exception ex)
			{
				Common.Log($"테이블 구조 복사 실패: {ex.Message}", LogType.Error);
				MessageBox.Show($"테이블 구조 복사 중 오류가 발생했습니다: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
			}
		}

		/// <summary>
		/// 테이블 구조 정보 생성 (컬럼 정보, 타입, 키 정보 등)
		/// </summary>
		private string GenerateTableStructure()
		{
			var sb = new StringBuilder();

			// 테이블 정보 헤더
			sb.AppendLine($"-- {SelectTableName}");
			//sb.AppendLine($"-- 생성일시: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
			//sb.AppendLine();

			// CREATE TABLE 문
			//sb.AppendLine("-- CREATE TABLE 문");
			//sb.AppendLine($"CREATE TABLE [{SelectTableName}]");
			//sb.AppendLine("(");

			var columns = _tableInfo.Columns.ToList();
			//for (int i = 0; i < columns.Count; i++)
			//{
			//	var column = columns[i];
			//	var isLast = i == columns.Count - 1;

			//	var columnDef = $"    [{column.Name}] {column.ToTypeString()}";

			//	// Null 여부
			//	if (column.IsNotNull)
			//		columnDef += " NOT NULL";

			//	// 키 마커
			//	if (column.IsKey)
			//		columnDef += " -- PK";

			//	sb.Append(columnDef);

			//	if (!isLast)
			//		sb.AppendLine(",");
			//}

			// Primary Key 제약조건 (있을 경우)
			var primaryKeys = columns.Where(x => x.IsKey).ToList();
			//if (primaryKeys.Any())
			//{
			//	sb.AppendLine(",");
			//	var pkColumns = string.Join(", ", primaryKeys.Select(x => $"[{x.Name}]"));
			//	sb.AppendLine($"    CONSTRAINT PK_{SelectTableName} PRIMARY KEY ({pkColumns})");
			//}

			//sb.AppendLine(");");
			//sb.AppendLine();

			// 컬럼 상세 정보
			//sb.AppendLine("-- 컬럼 상세 정보");
			sb.AppendLine("-- 컬럼명\t\t데이터타입\t\tNull여부\t기본값\t키\t설명");
			sb.AppendLine(new string('-', 80));

			foreach (var column in columns)
			{
				var nullable = column.IsNotNull ? "NOT NULL" : "NULL";
				var defaultValue = "-"; // Default 속성이 없으므로 하이픈으로 표시
				var key = column.IsKey ? "PK" : "-";
				var description = string.IsNullOrEmpty(column.Description) ? "-" : column.Description;

				var line = $"{column.Name,-20}\t{column.ToTypeString(),-15}\t{nullable,-8}\t{defaultValue,-15}\t{key,-3}\t{description}";
				sb.AppendLine(line);
			}

			sb.AppendLine();

			// 통계 정보
			//sb.AppendLine("-- 테이블 통계");
			//sb.AppendLine($"-- 총 컬럼 수: {columns.Count}");
			//sb.AppendLine($"-- 기본 키 컬럼 수: {primaryKeys.Count}");

			return sb.ToString();
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

			// EXEC/EXECUTE인 경우 SP 이름 추출
			int execIndex = Array.FindIndex(words, word =>
				word.Equals("exec", StringComparison.OrdinalIgnoreCase) ||
				word.Equals("execute", StringComparison.OrdinalIgnoreCase));

			if (execIndex != -1 && execIndex < words.Length - 1)
			{
				var spName = words[execIndex + 1];
				// 스키마 이름 제거 (예: dbo.sp_name -> sp_name)
				if (spName.Contains("."))
				{
					var parts = spName.Split('.');
					if (parts.Length > 1)
						spName = parts[^1]; // 마지막 부분만 사용
				}
				return spName;
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

		private string GetQueryStringFromFilter()
		{
			var builder = new StringBuilder($"SELECT * FROM {SelectTableName}" + Environment.NewLine);

			// Where 조건 추가
			var whereConditions = GetWhereConditions();
			if (whereConditions.Count > 0)
			{
				foreach (var condition in whereConditions)
				{
					var columnInfo = _tableInfo.Columns.First(x => x.Name.Equals(condition.Column));
					if (columnInfo == null) continue;

					if (columnInfo.TrueType == SqlDbType.Decimal)
					{
						builder.AppendLine($"{condition.LogicalOperator} {condition.Column} {condition.Operator} {condition.Value}");
					}
					else
					{
						// 문자열 값 처리: 빈 문자열, NULL, 따옴표가 있는 경우 등을 처리
						var value = condition.Value;

						// NULL 값 처리 (따옴표 없이)
						if (value.Trim().ToUpper() == "NULL")
						{
							builder.AppendLine($"{condition.LogicalOperator} {condition.Column} {condition.Operator} NULL");
						}
						// 빈 문자열 처리 ('' 그대로 사용)
						else if (value == "''")
						{
							builder.AppendLine($"{condition.LogicalOperator} {condition.Column} {condition.Operator} {value}");
						}
						// 이미 따옴표가 있는 경우 그대로 사용
						else if (value.StartsWith("'") && value.EndsWith("'"))
						{
							builder.AppendLine($"{condition.LogicalOperator} {condition.Column} {condition.Operator} {value}");
						}
						// 일반 문자열 (따옴표로 감싸기)
						else
						{
							builder.AppendLine($"{condition.LogicalOperator} {condition.Column} {condition.Operator} '{value}'");
						}
					}
				}
			}

			// OrderBy 조건 추가
			var orderConditions = GetOrderConditions();
			if (orderConditions.Count > 0)
			{
				builder.AppendLine("ORDER BY");
				for (int i = 0; i < orderConditions.Count; i++)
				{
					builder.Append($"{orderConditions[i].Column} {orderConditions[i].Direction}");
					if (i < orderConditions.Count - 1)
					{
						builder.Append(", ");
					}
				}
			}

			return builder.ToString();
		}

		/// <summary>
		/// Where 조건만 처리하는 쿼리 생성
		/// </summary>
		private string GetWhereQueryString()
		{
			var builder = new StringBuilder($"SELECT * FROM {SelectTableName}" + Environment.NewLine);
			var whereConditions = GetWhereConditions();

			for (int i = 0; i < whereConditions.Count; i++)
			{
				var condition = whereConditions[i];
				var columnInfo = _tableInfo.Columns.First(x => x.Name.Equals(condition.Column));
				if (columnInfo == null) continue;

				if (columnInfo.TrueType == SqlDbType.Decimal)
				{
					builder.AppendLine($"{condition.LogicalOperator} {condition.Column} {condition.Operator} {condition.Value}");
				}
				else
				{
					var value = condition.Value;

					// NULL 값 처리 (따옴표 없이)
					if (value.Trim().ToUpper() == "NULL")
					{
						builder.AppendLine($"{condition.LogicalOperator} {condition.Column} {condition.Operator} NULL");
					}
					// 빈 문자열 처리 ('' 그대로 사용)
					else if (value == "''")
					{
						builder.AppendLine($"{condition.LogicalOperator} {condition.Column} {condition.Operator} {value}");
					}
					// 이미 따옴표가 있는 경우 그대로 사용
					else if (value.StartsWith("'") && value.EndsWith("'"))
					{
						builder.AppendLine($"{condition.LogicalOperator} {condition.Column} {condition.Operator} {value}");
					}
					// 일반 문자열 (따옴표로 감싸기)
					else
					{
						builder.AppendLine($"{condition.LogicalOperator} {condition.Column} {condition.Operator} '{value}'");
					}
				}
			}

			return builder.ToString();
		}

		/// <summary>
		/// OrderBy 조건만 처리하는 쿼리 생성
		/// </summary>
		private string GetOrderQueryString()
		{
			var builder = new StringBuilder($"SELECT * FROM {SelectTableName}" + Environment.NewLine);
			var orderConditions = GetOrderConditions();

			if (orderConditions.Count > 0)
			{
				builder.AppendLine("ORDER BY");
				for (int i = 0; i < orderConditions.Count; i++)
				{
					builder.Append($"{orderConditions[i].Column} {orderConditions[i].Direction}");
					if (i < orderConditions.Count - 1)
					{
						builder.Append(", ");
					}
				}
			}

			return builder.ToString();
		}

		#region Where
		private void WhereCheckBox_Checked(object sender, RoutedEventArgs e)
		{
			WhereAddButton.IsEnabled = true;
			WherePanel.IsEnabled = true;

			if (wherePanels.Count == 0)
			{
				AddWherePanel();
			}
		}

		private void WhereCheckBox_Unchecked(object sender, RoutedEventArgs e)
		{
			WhereAddButton.IsEnabled = false;
			WherePanel.IsEnabled = false;
		}

		private StackPanel CreateWherePanel(int index)
		{
			var stackPanel = new StackPanel
			{
				Name = $"WherePanel_{index}",
				Orientation = Orientation.Horizontal,
				Margin = new Thickness(0, 5, 0, 0)
			};

			var columnComboBox = new ComboBox
			{
				Name = $"WhereColumnComboBox_{index}",
				Width = 120,
				Margin = new Thickness(0, 0, 5, 0)
			};
			PopulateWhereColumnComboBox(columnComboBox);

			var operatorButton = new Button
			{
				Name = $"WhereOperatorButton_{index}",
				Content = "=",
				Width = 32,
				Margin = new Thickness(0, 0, 5, 0)
			};
			operatorButton.Click += (sender, e) => WhereOperatorButton_Click(sender, e, index);

			var valueTextBox = new TextBox
			{
				Name = $"WhereValueTextBox_{index}",
				Width = 120,
				Margin = new Thickness(0, 0, 5, 0)
			};

			var deleteButton = new Button
			{
				Name = $"WhereDeleteButton_{index}",
				Content = "-",
				Width = 32,
				Margin = new Thickness(0, 0, 5, 0),
				Visibility = index > 1 ? Visibility.Visible : Visibility.Hidden
			};
			deleteButton.Click += (sender, e) => DeleteWherePanel_Click(sender, e, index);
			stackPanel.Children.Add(deleteButton);

			var andOrButton = new Button
			{
				Name = $"WhereAndOrButton_{index}",
				Content = index > 1 ? "AND" : "WHERE",
				Width = 50,
				Margin = new Thickness(0, 0, 5, 0),
				Visibility = index > 1 ? Visibility.Visible : Visibility.Hidden
			};
			andOrButton.Click += (sender, e) => WhereAndOrButton_Click(sender, e, index);
			stackPanel.Children.Add(andOrButton);

			stackPanel.Children.Add(columnComboBox);
			stackPanel.Children.Add(operatorButton);
			stackPanel.Children.Add(valueTextBox);

			return stackPanel;
		}

		public void AddWherePanel()
		{
			var newPanel = CreateWherePanel(wherePanelCounter);
			wherePanels.Add(newPanel);

			WherePanel.Children.Add(newPanel);

			wherePanelCounter++;
		}

		private void PopulateWhereColumnComboBox(ComboBox comboBox)
		{
			var columns = _tableInfo.Columns.Select(c => c.Name).ToList();

			comboBox.ItemsSource = columns;
			if (columns.Count > 0)
			{
				comboBox.SelectedIndex = 0;
			}
		}

		private List<WhereCondition> GetWhereConditions()
		{
			var conditions = new List<WhereCondition>();

			foreach (var panel in wherePanels)
			{
				var andOrButton = panel.Children.OfType<Button>().FirstOrDefault(b => b.Name.Contains("WhereAndOrButton"));
				var columnComboBox = panel.Children.OfType<ComboBox>().FirstOrDefault(cb => cb.Name.Contains("WhereColumnComboBox"));
				var operatorButton = panel.Children.OfType<Button>().FirstOrDefault(b => b.Name.Contains("WhereOperatorButton"));
				var valueTextBox = panel.Children.OfType<TextBox>().FirstOrDefault(tb => tb.Name.Contains("WhereValueTextBox"));

				if (columnComboBox?.SelectedItem != null)
				{
					// 빈 값일 경우 자동으로 '' 처리
					var value = valueTextBox?.Text;
					if (string.IsNullOrWhiteSpace(value))
					{
						value = "''";
					}

					conditions.Add(new WhereCondition
					{
						LogicalOperator = andOrButton?.Content.ToString() ?? "WHERE",
						Column = columnComboBox.SelectedItem.ToString() ?? string.Empty,
						Operator = operatorButton?.Content.ToString() ?? "=",
						Value = value
					});
				}
			}

			return conditions;
		}

		private void WhereAddButton_Click(object sender, RoutedEventArgs e)
		{
			AddWherePanel();
		}

		private void DeleteWherePanel_Click(object sender, RoutedEventArgs e, int index)
		{
			var button = sender as Button;

			if (button?.Parent is StackPanel panelToRemove)
			{
				WherePanel.Children.Remove(panelToRemove);
				wherePanels.Remove(panelToRemove);
				wherePanelCounter--;
			}
		}

		private void WhereAndOrButton_Click(object sender, RoutedEventArgs e, int index)
		{
			if (sender is Button button)
			{
				button.Content = button.Content.ToString() == "AND" ? "OR" : "AND";
			}
		}

		private void WhereOperatorButton_Click(object sender, RoutedEventArgs e, int index)
		{
			if (sender is Button button)
			{
				var operators = new[] { "=", "<>" };
				var currentIndex = Array.IndexOf(operators, button.Content.ToString());
				var nextIndex = (currentIndex + 1) % operators.Length;
				button.Content = operators[nextIndex];
			}
		}
		#endregion

		#region Order
		private void OrderCheckBox_Checked(object sender, RoutedEventArgs e)
		{
			OrderAddButton.IsEnabled = true;
			OrderPanel.IsEnabled = true;

			if (orderPanels.Count == 0)
			{
				AddOrderPanel();
			}
		}

		private void OrderCheckBox_Unchecked(object sender, RoutedEventArgs e)
		{
			OrderAddButton.IsEnabled = false;
			OrderPanel.IsEnabled = false;
		}

		private StackPanel CreateOrderPanel(int index)
		{
			var stackPanel = new StackPanel
			{
				Name = $"OrderPanel_{index}",
				Orientation = Orientation.Horizontal,
				Margin = new Thickness(0, 5, 0, 0)
			};

			var deleteButton = new Button
			{
				Name = $"OrderDeleteButton_{index}",
				Content = "-",
				Width = 32,
				Margin = new Thickness(0, 0, 5, 0),
				Visibility = index > 1 ? Visibility.Visible : Visibility.Hidden
			};
			deleteButton.Click += (sender, e) => DeleteOrderPanel_Click(sender, e, index);
			stackPanel.Children.Add(deleteButton);

			var columnComboBox = new ComboBox
			{
				Name = $"OrderColumnComboBox_{index}",
				Width = 120,
				Margin = new Thickness(0, 0, 5, 0)
			};
			PopulateOrderColumnComboBox(columnComboBox);

			var directionButton = new Button
			{
				Name = $"OrderDirectionButton_{index}",
				Content = "ASC",
				Width = 50,
				Margin = new Thickness(0, 0, 5, 0),
			};
			directionButton.Click += (sender, e) => OrderDirectionButton_Click(sender, e, index);

			stackPanel.Children.Add(columnComboBox);
			stackPanel.Children.Add(directionButton);

			return stackPanel;
		}

		public void AddOrderPanel()
		{
			var newPanel = CreateOrderPanel(orderPanelCounter);
			orderPanels.Add(newPanel);

			OrderPanel.Children.Add(newPanel);

			orderPanelCounter++;
		}

		private void PopulateOrderColumnComboBox(ComboBox comboBox)
		{
			var columns = _tableInfo.Columns.Select(c => c.Name).ToList();

			comboBox.ItemsSource = columns;
			if (columns.Count > orderPanelCounter - 1)
			{
				comboBox.SelectedIndex = orderPanelCounter - 1;
			}
			else
			{
				comboBox.SelectedIndex = columns.Count - 1;
			}
		}

		private List<OrderCondition> GetOrderConditions()
		{
			var conditions = new List<OrderCondition>();

			foreach (var panel in orderPanels)
			{
				var columnComboBox = panel.Children.OfType<ComboBox>().FirstOrDefault(cb => cb.Name.Contains("OrderColumnComboBox"));
				var directionButton = panel.Children.OfType<Button>().FirstOrDefault(b => b.Name.Contains("OrderDirectionButton"));

				if (columnComboBox?.SelectedItem != null)
				{
					conditions.Add(new OrderCondition
					{
						Column = columnComboBox.SelectedItem.ToString() ?? string.Empty,
						Direction = directionButton?.Content.ToString() ?? "ASC",
					});
				}
			}

			return conditions;
		}

		private void OrderAddButton_Click(object sender, RoutedEventArgs e)
		{
			AddOrderPanel();
		}

		private void DeleteOrderPanel_Click(object sender, RoutedEventArgs e, int index)
		{
			var button = sender as Button;

			if (button?.Parent is StackPanel panelToRemove)
			{
				OrderPanel.Children.Remove(panelToRemove);
				orderPanels.Remove(panelToRemove);
				orderPanelCounter--;
			}
		}

		private void OrderDirectionButton_Click(object sender, RoutedEventArgs e, int index)
		{
			if (sender is Button button)
			{
				button.Content = button.Content.ToString() == "ASC" ? "DESC" : "ASC";
			}
		}
		#endregion
	}
}
