using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;

using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace SqlServerWorkspace
{
	public static class TabManager
	{
		static string _monacoHtmlPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "index.html");
		static string _userDataFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SqlServerWorkspace");

		public static async Task CreateNewTab(this TabControl tabControl, string header)
		{
			var webView = new WebView2
			{
				HorizontalAlignment = HorizontalAlignment.Stretch,
				VerticalAlignment = VerticalAlignment.Stretch
			};

			try
			{
				var env = await CoreWebView2Environment.CreateAsync(null, _userDataFolder);
				await webView.EnsureCoreWebView2Async(env);
				webView.CoreWebView2.Settings.IsScriptEnabled = true;
				webView.CoreWebView2.NavigateToString(File.ReadAllText(_monacoHtmlPath));
			}
			catch (Exception ex)
			{

				throw;
			}
			

			var tabItem = new TabItem
			{
				Header = header,
				Content = webView
			};

			tabControl.Items.Add(tabItem);
		}

		public static TabItem? GetTabItem(this TabControl tabControl, string header)
		{
			foreach (TabItem tabItem in tabControl.Items)
			{
				if (tabItem.Header.ToString() == header)
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
