using Microsoft.Web.WebView2.Wpf;

using System.Text.Json;

namespace SqlServerWorkspace
{
	public static class WebViewManager
    {
        public static async Task<string> GetEditorText(this WebView2 webView)
        {
			string editorText = await webView.CoreWebView2.ExecuteScriptAsync("getEditorText();");
			return JsonSerializer.Deserialize<string>(editorText) ?? string.Empty;
		}

		public static async Task SetEditorText(this WebView2 webView, string text)
		{
			text = text.Replace("\r\n", "\\n").Replace("'", "\\'").Replace("\"", "\\\"");
			var script = $"setEditorText('{text}');";
			await webView.CoreWebView2.ExecuteScriptAsync(script);
		}

		public static async Task AppendEditorText(this WebView2 webView, string text)
		{
			text = text.Replace("\r\n", "\\n").Replace("'", "\\'").Replace("\"", "\\\"");
			var script = $"appendEditorText('{text}');";
			await webView.CoreWebView2.ExecuteScriptAsync(script);
		}

		public static async Task<string> GetSelectedText(this WebView2 webView)
		{
			string selectedText = await webView.CoreWebView2.ExecuteScriptAsync("getSelectedText();");
			return JsonSerializer.Deserialize<string>(selectedText) ?? string.Empty;
		}
	}
}
