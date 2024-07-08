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

		public static async Task SetWebView(this TabControl tabControl, WebView2 webView)
		{
			var env = await CoreWebView2Environment.CreateAsync(null, _userDataFolder);
			await webView.EnsureCoreWebView2Async(env);
			webView.CoreWebView2.Settings.IsScriptEnabled = true;
			webView.CoreWebView2.NavigateToString(File.ReadAllText(_monacoHtmlPath));
		}

		public static async Task CreateNewOrOpenTab(this TabControl tabControl, TreeNode treeNode)
		{
			var header = treeNode.Name;
			var type = treeNode.Type;
			var tabItem = GetTabItem(tabControl, header);

			// Open
			if (tabItem != null)
			{
				tabItem.IsSelected = true;
				return;
			}

			// Create New
			var newTabItem = new TabItem
			{
				Header = new TextBlock { Text = header }
			};
			tabControl.Items.Add(newTabItem);

			switch (type)
			{
				case TreeNodeType.TableNode:
					var dataGrid = new DataGrid
					{
						IsReadOnly = true,
						ItemsSource = SqlManager.Select("*", header).DefaultView
					};
					newTabItem.Content = dataGrid;
					newTabItem.IsSelected = true;
					newTabItem.UpdateLayout();
					break;

				case TreeNodeType.ViewNode:
				case TreeNodeType.FunctionNode:
				case TreeNodeType.ProcedureNode:
					var webView = new WebView2
					{
						HorizontalAlignment = HorizontalAlignment.Stretch,
						VerticalAlignment = VerticalAlignment.Stretch
					};

					newTabItem.Content = webView;
					newTabItem.IsSelected = true;
					newTabItem.UpdateLayout();

					var env = await CoreWebView2Environment.CreateAsync(null, _userDataFolder);
					await webView.EnsureCoreWebView2Async(env);
					webView.CoreWebView2.Settings.IsScriptEnabled = true;
					webView.CoreWebView2.NavigateToString(File.ReadAllText(_monacoHtmlPath));

					webView.NavigationCompleted += async (sender, args) =>
					{
						if (args.IsSuccess)
						{
							var text = SqlManager.GetObject(header).Replace("\r\n", "\\n").Replace("'", "\\'").Replace("\"", "\\\"");
							await SetEditorText(tabControl, header, text);
						}
					};
					break;

				default:
					break;
			}
		}

		public static TabItem? GetTabItem(this TabControl tabControl, string header)
		{
			foreach (TabItem tabItem in tabControl.Items)
			{
				if ((tabItem.Header as TextBlock)?.Text == header)
				{
					return tabItem;
				}
			}
			return null;
		}

		public static async Task SetEditorText(this TabControl tabControl, string tabHeader, string text)
		{
			var tabItem = tabControl.GetTabItem(tabHeader);

			if (tabItem == null)
			{
				return;
			}

			if (tabItem.Content is not WebView2 textEditorView)
			{
				return;
			}

			var script = $"setEditorText('{text}');";
			await textEditorView.CoreWebView2.ExecuteScriptAsync(script);
		}
	}
}
