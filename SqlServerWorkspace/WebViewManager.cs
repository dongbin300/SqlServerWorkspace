using AvalonDock.Layout;

using Microsoft.Web.WebView2.Wpf;

using SqlServerWorkspace.Data;

using System.Text.Json;
using System.Windows.Input;

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
			// 줄 바꿈 문자 정규화: 모든 줄 바꿈 문자를 \r\n으로 통일
			text = text.Replace("\r\n", "\n").Replace("\r", "\n").Replace("\n", "\r\n");

			// JavaScript 문자열 이스케이프 처리
			text = text.Replace("\r\n", "\\n").Replace("'", "\\'").Replace("\"", "\\\"");
			var script = $"setEditorText('{text}');";
			await webView.CoreWebView2.ExecuteScriptAsync(script);
		}

		public static async Task AppendEditorText(this WebView2 webView, string text)
		{
			// 줄 바꿈 문자 정규화: 모든 줄 바꿈 문자를 \r\n으로 통일
			text = text.Replace("\r\n", "\n").Replace("\r", "\n").Replace("\n", "\r\n");

			// JavaScript 문자열 이스케이프 처리
			text = text.Replace("\r\n", "\\n").Replace("'", "\\'").Replace("\"", "\\\"");
			var script = $"appendEditorText('{text}');";
			await webView.CoreWebView2.ExecuteScriptAsync(script);
		}

		public static async Task<string> GetSelectedText(this WebView2 webView)
		{
			string selectedText = await webView.CoreWebView2.ExecuteScriptAsync("getSelectedText();");
			return JsonSerializer.Deserialize<string>(selectedText) ?? string.Empty;
		}

		public static async Task SetAutocompleteData(this WebView2 webView, List<AutocompletionItem> items)
		{
			try
				{
				var json = JsonSerializer.Serialize(items);
				// JSON 문자열을 안전하게 이스케이프
				var escapedJson = json.Replace("\\", "\\\\").Replace("'", "\\'").Replace("\"", "\\\"").Replace("\r", "\\r").Replace("\n", "\\n");
				var script = $"setAutocompleteData('{escapedJson}');";
				await webView.CoreWebView2.ExecuteScriptAsync(script);
			}
			catch (Exception ex)
			{
				System.Diagnostics.Debug.WriteLine($"Error setting autocomplete data: {ex.Message}");
			}
		}

		public static async Task SetTableColumns(this WebView2 webView, string tableName, List<string> columns)
		{
			try
			{
				var json = JsonSerializer.Serialize(columns);
				// JSON 문자열을 안전하게 이스케이프
				var escapedJson = json.Replace("\\", "\\\\").Replace("'", "\\'").Replace("\"", "\\\"").Replace("\r", "\\r").Replace("\n", "\\n");
				var script = $"setTableColumns('{tableName}', JSON.parse('{escapedJson}'));";
				await webView.CoreWebView2.ExecuteScriptAsync(script);
			}
			catch (Exception ex)
			{
				System.Diagnostics.Debug.WriteLine($"Error setting table columns: {ex.Message}");
			}
		}

		public static void AddReferenceAnalysisKeyBinding(this WebView2 webView, LayoutDocumentPane layoutDocumentPane, SqlManager manager, string procedureName)
		{
			webView.KeyDown += async (s, e) =>
			{
				if (e.Key == Key.F6)
				{
					await layoutDocumentPane.AnalyzeAndAddReferences(manager, procedureName);
				}
			};
		}

		public static void UpdateExistingKeyDownHandler(this WebView2 webView, LayoutDocumentPane layoutDocumentPane, SqlManager manager, string nodeHeader)
		{
			// 기존 KeyDown 이벤트에 case 추가
			/*
			case Key.F6: // Analyze References
				if (nodeType == TreeNodeType.ProcedureNode)
				{
					await layoutDocumentPane.AnalyzeAndAddReferences(manager, nodeHeader);
				}
				break;
			*/
		}
	}
}
