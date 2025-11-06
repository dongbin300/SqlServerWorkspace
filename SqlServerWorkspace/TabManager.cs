using AvalonDock.Layout;

using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;

using SqlServerWorkspace.Data;
using SqlServerWorkspace.DataModels;
using SqlServerWorkspace.Enums;
using SqlServerWorkspace.Extensions;
using SqlServerWorkspace.Views.Controls;
using SqlServerWorkspace.Views.CustomControls;

using System.Data;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace SqlServerWorkspace
{
	public static partial class TabManager
	{
		static readonly string _monacoHtmlPath = Path.Combine("Resources", "monaco.html");
		static readonly string _userDataFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SqlServerWorkspace");
		static bool _isFirstNewTab = true;

		public static async Task Init(this WebView2 webView)
		{
			try
			{
				var env = await CoreWebView2Environment.CreateAsync(null, _userDataFolder);
				await webView.EnsureCoreWebView2Async(env);
				webView.CoreWebView2.Settings.IsScriptEnabled = true;
				webView.CoreWebView2.NavigateToString(File.ReadAllText(_monacoHtmlPath));
			}
			catch (ArgumentException)
			{

			}
		}

		public static LayoutDocument? GetCurrentTab(this LayoutDocumentPane layoutDocumentPane)
		{
			ArgumentNullException.ThrowIfNull(layoutDocumentPane);

			if (layoutDocumentPane.SelectedContent is LayoutDocument selectedDocument)
			{
				return selectedDocument;
			}

			return null;
		}

		public static async Task CreateNewOrOpenTab(this LayoutDocumentPane layoutDocumentPane, SqlManager manager, string nodeHeader, TreeNodeType nodeType)
		{
			var layoutContent = GetLayoutContent(layoutDocumentPane, nodeHeader);

			// Open
			if (layoutContent != null)
			{
				layoutContent.IsSelected = true;
				return;
			}

			// Create New
			var newLayoutContent = new LayoutDocument
			{
				Title = nodeHeader
			};
			if (_isFirstNewTab)
			{
				_isFirstNewTab = false;
				layoutDocumentPane.Children.Clear();
			}
			layoutDocumentPane.Children.Add(newLayoutContent);

			switch (nodeType)
			{
				case TreeNodeType.TableNode:
					var tableViewControl = new TableViewControl()
					{
						Manager = manager,
						Header = nodeHeader
					};

					newLayoutContent.Content = tableViewControl;
					newLayoutContent.IsSelected = true;
					break;

				case TreeNodeType.DatabaseNode:
				case TreeNodeType.ViewNode:
				case TreeNodeType.FunctionNode:
				case TreeNodeType.ProcedureNode:
					var webView = new WebView2
					{
						HorizontalAlignment = HorizontalAlignment.Stretch,
						VerticalAlignment = VerticalAlignment.Stretch
					};
					webView.KeyDown += async (s, e) =>
					{
						switch (e.Key)
						{
							case Key.F5: // Run Script
								try
								{
									var selectedText = await webView.GetSelectedText();
									var text = string.IsNullOrEmpty(selectedText) ? await webView.GetEditorText() : selectedText;
									text = text.Replace("\n", "\r\n");
									var parts = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
									var firstKeyword = parts[0].Trim();

									if (firstKeyword.Equals("exec", StringComparison.OrdinalIgnoreCase))
									{
										var procedureName = parts[1].Trim().Replace("[", "").Replace("]", "");

										// Add Parameters
										var parameters = new Dictionary<string, string>();
										var parameterNames = manager.GetProcedureParameterNames(procedureName);
										if (parameterNames.Any())
										{
											var parameterString = text[text.IndexOf(parts[2][0])..].Trim();
											var parameterParts = parameterString.Split(',');
											for (int i = 0; i < parameterParts.Length; i++)
											{
												parameters.Add(parameterNames.ElementAt(i), parameterParts[i].Trim().Trim('\''));
											}
										}

										// Execute SP with multiple query support to capture PRINT messages
										var sb = new System.Text.StringBuilder();
										sb.AppendLine($"EXEC {procedureName}");

										if (parameters != null && parameters.Count > 0)
										{
											var paramList = parameters.Select(p => $"'{p.Value}'");
											sb.AppendLine(string.Join(", ", paramList));
										}

										var multipleResults = manager.ExecuteMultipleQueries(sb.ToString());

										if (multipleResults.HasError)
										{
											Common.Log(multipleResults.ErrorMessage, LogType.Error);
											break;
										}

										var mainWindowStatusPanel = ((MainWindow)Common.MainWindow).StatusPanel;

										// SP 결과 테이블 표시
										for (int i = 0; i < multipleResults.Tables.Count; i++)
										{
											var table = multipleResults.Tables[i];
											var contentId = $"RESULT_{i}";

											var anchorables = mainWindowStatusPanel.Children.OfType<LayoutAnchorable>().Where(a => a.ContentId == contentId);

											if (anchorables.Any())
											{
												var anchorable = anchorables.First();
												var gridWithCount = CreateDataGridWithRowCount(table, "DarkDataGridSimple");
												anchorable.Content = gridWithCount;
											}
											else
											{
												var tablePanel = new LayoutAnchorable()
												{
													ContentId = contentId,
													Title = $"Result {i + 1}"
												};
												var gridWithCount = CreateDataGridWithRowCount(table, "DarkDataGridSimple");
												tablePanel.Content = gridWithCount;
												mainWindowStatusPanel.Children.Add(tablePanel);
											}
										}

										// SP 내부 PRINT 메시지 표시
										if (multipleResults.HasMessages)
										{
											var messageTable = new DataTable();
											messageTable.Columns.Add("Message");

											foreach (var message in multipleResults.Messages)
											{
												messageTable.Rows.Add(message);
											}

											var anchorables = mainWindowStatusPanel.Children.OfType<LayoutAnchorable>().Where(a => a.ContentId == "MSG");

											if (anchorables.Any())
											{
												var anchorable = anchorables.First();
												var gridWithCount = CreateDataGridWithRowCount(messageTable, "DarkDataGridSimple");
												anchorable.Content = gridWithCount;
											}
											else
											{
												var messagePanel = new LayoutAnchorable()
												{
													ContentId = "MSG",
													Title = "Messages"
												};
												var gridWithCount = CreateDataGridWithRowCount(messageTable, "DarkDataGridSimple");
												messagePanel.Content = gridWithCount;
												mainWindowStatusPanel.Children.Add(messagePanel);
											}
										}

										if (multipleResults.HasTables)
										{
											Common.SetStatusPanelSelectedIndex("RESULT_0");
										}
										else if (multipleResults.HasMessages)
										{
											Common.SetStatusPanelSelectedIndex("MSG");
										}
										else
										{
											// 결과가 없는 경우 성공 메시지 표시
											if (multipleResults.HasAffectedRows)
											{
												var totalAffected = multipleResults.TotalRecordsAffected;
												if (totalAffected > 0)
													Common.Log($"{totalAffected} row(s) affected", LogType.Success);
												else
													Common.Log("Stored Procedure executed successfully", LogType.Success);
											}
											else
											{
												Common.Log("Stored Procedure executed successfully", LogType.Success);
											}
										}
									}
									else
									{
										// 여러 쿼리 실행 (SELECT, PRINT 등 모두 처리)
										var multipleResults = manager.ExecuteMultipleQueries(text);

										if (multipleResults.HasError)
										{
											Common.Log(multipleResults.ErrorMessage, LogType.Error);
											break;
										}

										var mainWindowStatusPanel = ((MainWindow)Common.MainWindow).StatusPanel;

										// 모든 테이블 결과 표시
										for (int i = 0; i < multipleResults.Tables.Count; i++)
										{
											var table = multipleResults.Tables[i];
											var resultTitle = $"Result {i + 1}";
											var contentId = $"RESULT_{i}";

											var anchorables = mainWindowStatusPanel.Children.OfType<LayoutAnchorable>().Where(a => a.ContentId == contentId);

											if (anchorables.Any())
											{
												var anchorable = anchorables.First();
												var gridWithCount = CreateDataGridWithRowCount(table, "DarkDataGridSimple");
												anchorable.Content = gridWithCount;
											}
											else
											{
												var tablePanel = new LayoutAnchorable()
												{
													ContentId = contentId,
													Title = resultTitle
												};
												var gridWithCount = CreateDataGridWithRowCount(table, "DarkDataGridSimple");
												tablePanel.Content = gridWithCount;
												mainWindowStatusPanel.Children.Add(tablePanel);
											}
										}

										// PRINT 메시지를 Result 탭에 표시
										if (multipleResults.HasMessages)
										{
											var messageTable = new DataTable();
											messageTable.Columns.Add("Message");

											foreach (var message in multipleResults.Messages)
											{
												messageTable.Rows.Add(message);
											}

											var messageTitle = multipleResults.HasTables ? "Messages" : "Result";
											var messageId = multipleResults.HasTables ? $"MSG" : "RESULT_0";

											var anchorables = mainWindowStatusPanel.Children.OfType<LayoutAnchorable>().Where(a => a.ContentId == messageId);

											if (anchorables.Any())
											{
												var anchorable = anchorables.First();
												var gridWithCount = CreateDataGridWithRowCount(messageTable, "DarkDataGridSimple");
												anchorable.Content = gridWithCount;
											}
											else
											{
												var tablePanel = new LayoutAnchorable()
												{
													ContentId = messageId,
													Title = messageTitle
												};
												var gridWithCount = CreateDataGridWithRowCount(messageTable, "DarkDataGridSimple");
												tablePanel.Content = gridWithCount;
												mainWindowStatusPanel.Children.Add(tablePanel);
											}
										}

										// 마지막 결과 탭을 선택
										if (multipleResults.HasTables)
										{
											var lastContentId = $"RESULT_{multipleResults.Tables.Count - 1}";
											Common.SetStatusPanelSelectedIndex(lastContentId);
										}
										else if (multipleResults.HasMessages)
										{
											Common.SetStatusPanelSelectedIndex("MSG");
										}
										else
										{
											// DDL 명령어 등 결과셋이 없는 경우 성공 메시지 표시
											if (multipleResults.HasAffectedRows)
											{
												var totalAffected = multipleResults.TotalRecordsAffected;
												if (totalAffected > 0)
													Common.Log($"{totalAffected} row(s) affected", LogType.Success);
												else
													Common.Log("Command completed successfully", LogType.Success);
											}
											else
											{
												Common.Log("Command completed successfully", LogType.Success);
											}
										}
									}
								}
								catch (Exception ex)
								{
									Common.Log(ex.Message, LogType.Error);
								}
								break;

							default:
								break;
						}
					};

					newLayoutContent.Content = webView;
					newLayoutContent.IsSelected = true;

					await webView.Init();

					webView.NavigationCompleted += async (sender, args) =>
					{
						if (args.IsSuccess)
						{
							var text = manager.GetObject(nodeHeader);
							// 줄 바꿈 문자 정규화: SQL Server에서 가져온 텍스트의 줄 바꿈을 통일
							text = text.Replace("\r\n", "\n").Replace("\r", "\n").Replace("\n", "\r\n");
							text = CreateProcedureRegex().Replace(text, "ALTER PROCEDURE");
							text = CreateFunctionRegex().Replace(text, "ALTER FUNCTION");
							await layoutDocumentPane.SetEditorText(nodeHeader, text);

							List<AutocompletionItem> autocompleteItems = [];

							// 테이블 자동완성 항목 추가
							var tableNames = manager.SelectTableNames();
							autocompleteItems.AddRange(tableNames.Select(x => new AutocompletionItem(x, 6, x, x, "Table", x)));

							// 프로시저 자동완성 항목 추가
							var procedureNames = manager.SelectProcedureNames();
							autocompleteItems.AddRange(procedureNames.Select(x => new AutocompletionItem(x, 10, x, x, "Stored Procedure", x)));

							// 함수 자동완성 항목 추가
							var functionNames = manager.SelectFunctionNames();
							autocompleteItems.AddRange(functionNames.Select(x => new AutocompletionItem(x, 2, x + "()", x + "()", "Function", x + "()")));

							await layoutDocumentPane.SetAutocompleteData(nodeHeader, autocompleteItems);

							// 모든 테이블의 컬럼 정보 미리 로드
							foreach (var tableName in tableNames)
							{
								var columns = manager.GetTableColumns(tableName);
								if (columns.Count > 0)
								{
									await layoutDocumentPane.SetTableColumns(nodeHeader, tableName, columns);
								}
							}

							// Add references object
							if (nodeType == TreeNodeType.ProcedureNode)
							{
								await layoutDocumentPane.AnalyzeAndAddReferences(manager, nodeHeader);
							}

							Common.Log(nodeHeader, LogType.Info);
						}
					};
					break;

				default:
					break;
			}
		}

		public static async Task CreateNewOrOpenTab(this LayoutDocumentPane layoutDocumentPane, SqlManager manager, TreeNode treeNode)
		{
			var header = treeNode.Name;
			var type = treeNode.Type;

			await CreateNewOrOpenTab(layoutDocumentPane, manager, header, type);
		}

		public static LayoutContent? GetLayoutContent(this LayoutDocumentPane layoutDocumentPane, string header)
		{
			if (string.IsNullOrWhiteSpace(header))
				return null;

			var normalizedHeader = header.Trim();

			foreach (var layoutContent in layoutDocumentPane.Children)
			{
				// 대소문자 무시하고 공백 제거해서 비교
				if (string.Equals(layoutContent.Title?.Trim(), normalizedHeader, StringComparison.OrdinalIgnoreCase))
				{
					return layoutContent;
				}
			}
			return null;
		}

		public static async Task SetEditorText(this LayoutDocumentPane layoutDocumentPane, string title, string text)
		{
			var layoutContent = layoutDocumentPane.GetLayoutContent(title);

			if (layoutContent == null)
			{
				return;
			}

			if (layoutContent.Content is not WebView2 webView)
			{
				return;
			}

			await webView.SetEditorText(text);
		}

		public static async Task SetAutocompleteData(this LayoutDocumentPane layoutDocumentPane, string title, List<AutocompletionItem> items)
		{
			var layoutContent = layoutDocumentPane.GetLayoutContent(title);

			if (layoutContent == null)
			{
				return;
			}

			if (layoutContent.Content is not WebView2 webView)
			{
				return;
			}

			await webView.SetAutocompleteData(items);
		}

		public static async Task SetTableColumns(this LayoutDocumentPane layoutDocumentPane, string title, string tableName, List<string> columns)
		{
			var layoutContent = layoutDocumentPane.GetLayoutContent(title);

			if (layoutContent == null)
			{
				return;
			}

			if (layoutContent.Content is not WebView2 webView)
			{
				return;
			}

			await webView.SetTableColumns(tableName, columns);
		}

		/// <summary>
		/// 데이터그리드와 행 수 표시를 포함하는 컨테이너를 생성
		/// </summary>
		private static Grid CreateDataGridWithRowCount(DataTable table, string styleKey)
		{
			var grid = new Grid();
			grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
			grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

			var dataGrid = new AdvanceDataGrid()
			{
				Style = (Style)Application.Current.Resources[styleKey],
				ItemsSource = table.DefaultView
			};
			dataGrid.FillSqlDataTableSimple(table);
			grid.Children.Add(dataGrid);

			var rowCountBorder = new Border
			{
				Background = Application.Current.Resources["DarkBackground"] as System.Windows.Media.Brush,
				BorderBrush = Application.Current.Resources["DarkForeground"] as System.Windows.Media.Brush,
				BorderThickness = new Thickness(0, 1, 0, 0),
				Padding = new Thickness(8, 4, 8, 4)
			};
			Grid.SetRow(rowCountBorder, 1);

			var rowCountText = new TextBlock
			{
				Text = $"총 {table.Rows.Count:N0}건",
				Foreground = Application.Current.Resources["DarkForeground"] as System.Windows.Media.Brush,
				FontSize = 12,
				HorizontalAlignment = HorizontalAlignment.Right
			};
			rowCountBorder.Child = rowCountText;

			grid.Children.Add(rowCountBorder);
			return grid;
		}

		[GeneratedRegex(@"\bcreate\s+procedure\b", RegexOptions.IgnoreCase)]
		private static partial Regex CreateProcedureRegex();
		[GeneratedRegex(@"\bcreate\s+function\b", RegexOptions.IgnoreCase)]
		private static partial Regex CreateFunctionRegex();
	}
}
