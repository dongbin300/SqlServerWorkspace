using AvalonDock.Layout;

using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;

using SqlServerWorkspace.Data;
using SqlServerWorkspace.DataModels;
using SqlServerWorkspace.Enums;
using SqlServerWorkspace.Extensions;
using SqlServerWorkspace.Views.Controls;

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

										// Execute SP
										var table = manager.ExecuteStoredProcedure(procedureName, parameters);

										var mainWindowStatusPanel = ((MainWindow)Common.MainWindow).StatusPanel;
										var anchorables = mainWindowStatusPanel.Children.OfType<LayoutAnchorable>().Where(a => a.ContentId == "SPER");

										if (anchorables.Any())
										{
											var anchorable = anchorables.First();
											var dataGrid = new DataGrid()
											{
												Style = (Style)Application.Current.Resources["DarkDataGridSimple"],
												ItemsSource = table.DefaultView
											};
											dataGrid.FillSqlDataTableSimple(table);

											anchorable.Content = dataGrid;
										}
										else
										{
											var tablePanel = new LayoutAnchorable()
											{
												ContentId = "SPER",
												Title = "Stored Procedure Execute Result"
											};
											var dataGrid = new DataGrid()
											{
												Style = (Style)Application.Current.Resources["DarkDataGridSimple"],
												ItemsSource = table.DefaultView
											};
											dataGrid.FillSqlDataTableSimple(table);

											tablePanel.Content = dataGrid;
											mainWindowStatusPanel.Children.Add(tablePanel);
										}

										Common.SetStatusPanelSelectedIndex("SPER");
									}
									else if (firstKeyword.Equals("select", StringComparison.OrdinalIgnoreCase))
									{
										var table = manager.Select(text);

										var mainWindowStatusPanel = ((MainWindow)Common.MainWindow).StatusPanel;
										var anchorables = mainWindowStatusPanel.Children.OfType<LayoutAnchorable>().Where(a => a.ContentId == "SR");

										if (anchorables.Any())
										{
											var anchorable = anchorables.First();
											var dataGrid = new DataGrid()
											{
												Style = (Style)Application.Current.Resources["DarkDataGridSimple"],
												ItemsSource = table.DefaultView
											};
											dataGrid.FillSqlDataTableSimple(table);

											anchorable.Content = dataGrid;
										}
										else
										{
											var tablePanel = new LayoutAnchorable()
											{
												ContentId = "SR",
												Title = "Result"
											};
											var dataGrid = new DataGrid()
											{
												Style = (Style)Application.Current.Resources["DarkDataGridSimple"],
												ItemsSource = table.DefaultView
											};
											dataGrid.FillSqlDataTableSimple(table);

											tablePanel.Content = dataGrid;
											mainWindowStatusPanel.Children.Add(tablePanel);
										}

										Common.SetStatusPanelSelectedIndex("SR");
									}
									else
									{
										var result = manager.Execute(text);
										if (!string.IsNullOrEmpty(result))
										{
											Common.Log(result, LogType.Error);
											break;
										}

										Common.Log("Run Complete", LogType.Success);
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
							text = CreateProcedureRegex().Replace(text, "ALTER PROCEDURE");
							text = CreateFunctionRegex().Replace(text, "ALTER FUNCTION");
							await layoutDocumentPane.SetEditorText(nodeHeader, text);
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
			foreach (var layoutContent in layoutDocumentPane.Children)
			{
				if (layoutContent.Title == header)
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

		[GeneratedRegex(@"\bcreate\s+procedure\b", RegexOptions.IgnoreCase)]
		private static partial Regex CreateProcedureRegex();
		[GeneratedRegex(@"\bcreate\s+function\b", RegexOptions.IgnoreCase)]
		private static partial Regex CreateFunctionRegex();
	}
}
