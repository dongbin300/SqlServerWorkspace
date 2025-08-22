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

				Common.Log($"{query}", LogType.Info);
			}
			catch (Exception ex)
			{
				Common.Log(ex.Message, LogType.Error);
			}
		}

		private async void RunButton_Click(object sender, RoutedEventArgs e)
		{
			var query = WhereCheckBox.IsChecked == true || OrderCheckBox.IsChecked == true ? GetQueryStringFromFilter() : await WebView.GetEditorText();

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
			var whereConditions = GetWhereConditions();

			for (int i = 0; i < whereConditions.Count; i++)
			{
				var columnInfo = _tableInfo.Columns.First(x => x.Name.Equals(whereConditions[i].Column));
				if (columnInfo == null)
				{
					continue;
				}

				if (columnInfo.TrueType == SqlDbType.Decimal)
				{
					builder.AppendLine($"{whereConditions[i].LogicalOperator} {whereConditions[i].Column} {whereConditions[i].Operator} {whereConditions[i].Value}");
				}
				else
				{
					builder.AppendLine($"{whereConditions[i].LogicalOperator} {whereConditions[i].Column} {whereConditions[i].Operator} '{whereConditions[i].Value}'");
				}
			}

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

				if (columnComboBox?.SelectedItem != null && !string.IsNullOrWhiteSpace(valueTextBox?.Text))
				{
					conditions.Add(new WhereCondition
					{
						LogicalOperator = andOrButton?.Content.ToString() ?? "WHERE",
						Column = columnComboBox.SelectedItem.ToString() ?? string.Empty,
						Operator = operatorButton?.Content.ToString() ?? "=",
						Value = valueTextBox.Text
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
