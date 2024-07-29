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

		public static async Task CreateNewOrOpenTab(this LayoutDocumentPane layoutDocumentPane, SqlManager manager, TreeNode treeNode)
		{
			var header = treeNode.Name;
			var type = treeNode.Type;
			var layoutContent = GetLayoutContent(layoutDocumentPane, header);

			// Open
			if (layoutContent != null)
			{
				layoutContent.IsSelected = true;
				return;
			}

			// Create New
			var newLayoutContent = new LayoutDocument
			{
				Title = header
			};
			layoutDocumentPane.Children.Add(newLayoutContent);

			switch (type)
			{
				case TreeNodeType.TableNode:
					var tableViewControl = new TableViewControl()
					{
						Manager = manager,
						Header = header
					};

					newLayoutContent.Content = tableViewControl;
					newLayoutContent.IsSelected = true;
					break;

				case TreeNodeType.ViewNode:
				case TreeNodeType.FunctionNode:
				case TreeNodeType.ProcedureNode:
					//var grid = new DockPanel() { LastChildFill = true };
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
								var selectedText = await webView.GetSelectedText();
								var text = string.IsNullOrEmpty(selectedText) ? await webView.GetEditorText() : selectedText;
								text = text.Replace("\n", "\r\n");

								if (text.Split(' ')[0].Equals("exec", StringComparison.OrdinalIgnoreCase))
								{
									var execText = text[(text.IndexOf("exec", StringComparison.OrdinalIgnoreCase) + 4)..].Trim();
									var table = manager.ExecuteStoredProcedure(execText);

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

									var anchorable2 = mainWindowStatusPanel.Children.OfType<LayoutAnchorable>().First(a => a.ContentId == "SPER");
									mainWindowStatusPanel.SelectedContentIndex = mainWindowStatusPanel.Children.IndexOf(anchorable2);
								}
								else
								{
									var result = manager.Execute(text);
									if (!string.IsNullOrEmpty(result))
									{
										Common.Log(result, LogType.Error);
										break;
									}
								}

								Common.Log("Run Complete", LogType.Success);
								break;

							default:
								break;
						}
					};

					//grid.Children.Add(webView);
					newLayoutContent.Content = webView;
					newLayoutContent.IsSelected = true;

					await webView.Init();

					webView.NavigationCompleted += async (sender, args) =>
					{
						if (args.IsSuccess)
						{
							var text = manager.GetObject(header);
							text = CreateProcedureRegex().Replace(text, "ALTER PROCEDURE");
							text = CreateFunctionRegex().Replace(text, "ALTER FUNCTION");
							await layoutDocumentPane.SetEditorText(header, text);
							Common.Log(header, LogType.Info);
						}
					};
					break;

				default:
					break;
			}
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
