using Microsoft.Web.WebView2.Wpf;

namespace SqlServerWorkspace
{
	public static class WebViewManager
    {
        public static async Task<string> GetEditorText(this WebView2 webView)
        {
			string editorText = await webView.CoreWebView2.ExecuteScriptAsync("getEditorText();");
			return System.Text.Json.JsonSerializer.Deserialize<string>(editorText) ?? string.Empty;
		}
    }
}
