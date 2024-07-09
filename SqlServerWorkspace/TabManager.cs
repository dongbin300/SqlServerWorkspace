using AvalonDock.Layout;

using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;

using SqlServerWorkspace.DataModels;
using SqlServerWorkspace.Enums;

using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace SqlServerWorkspace
{
	public static class TabManager
	{
		static readonly string _monacoHtmlPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "index.html");
		static readonly string _userDataFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SqlServerWorkspace");

		public static async Task CreateNewOrOpenTab(this LayoutDocumentPane layoutDocumentPane, TreeNode treeNode)
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
					var dataGrid = new DataGrid
					{
						IsReadOnly = true,
						ItemsSource = SqlManager.Select("*", header).DefaultView,
						Style = (Style)Application.Current.FindResource("LightDataGrid")
					};
					newLayoutContent.Content = dataGrid;
					newLayoutContent.IsSelected = true;
					break;

				case TreeNodeType.ViewNode:
				case TreeNodeType.FunctionNode:
				case TreeNodeType.ProcedureNode:
					var webView = new WebView2
					{
						HorizontalAlignment = HorizontalAlignment.Stretch,
						VerticalAlignment = VerticalAlignment.Stretch
					};

					newLayoutContent.Content = webView;
					newLayoutContent.IsSelected = true;

					var env = await CoreWebView2Environment.CreateAsync(null, _userDataFolder);
					await webView.EnsureCoreWebView2Async(env);
					webView.CoreWebView2.Settings.IsScriptEnabled = true;
					webView.CoreWebView2.NavigateToString(File.ReadAllText(_monacoHtmlPath));

					webView.NavigationCompleted += async (sender, args) =>
					{
						if (args.IsSuccess)
						{
							var text = SqlManager.GetObject(header).Replace("\r\n", "\\n").Replace("'", "\\'").Replace("\"", "\\\"");
							await SetEditorText(layoutDocumentPane, header, text);
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

			if (layoutContent.Content is not WebView2 textEditorView)
			{
				return;
			}

			var script = $"setEditorText('{text}');";
			await textEditorView.CoreWebView2.ExecuteScriptAsync(script);
		}
	}
}
