using AvalonDock.Layout;

using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;

using SqlServerWorkspace.Data;
using SqlServerWorkspace.DataModels;
using SqlServerWorkspace.Enums;
using SqlServerWorkspace.Views.Controls;

using System.Data;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;

namespace SqlServerWorkspace
{
	public static partial class TabManager
	{
		static readonly string _monacoHtmlPath = Path.Combine("Resources", "monaco.html");
		static readonly string _userDataFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SqlServerWorkspace");

		public static async Task Init(this WebView2 webView)
		{
			var env = await CoreWebView2Environment.CreateAsync(null, _userDataFolder);
			await webView.EnsureCoreWebView2Async(env);
			webView.CoreWebView2.Settings.IsScriptEnabled = true;
			webView.CoreWebView2.NavigateToString(File.ReadAllText(_monacoHtmlPath));
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
					//var table = manager.Select("*", header);
					//var dataGrid = new DataGrid
					//{
					//	ItemsSource = table.DefaultView,
					//	Style = (Style)Application.Current.FindResource("DarkDataGrid")
					//};

					//foreach (DataColumn column in table.Columns)
					//{
					//	var binding = new Binding(column.ColumnName)
					//	{
					//		//Converter = (IValueConverter)Application.Current.FindResource("DBNullToNullStringConverter")
					//	};

					//	if (column.DataType == typeof(DateTime))
					//	{
					//		binding.StringFormat = "yyyy-MM-dd HH:mm:ss";
					//	}

					//	var dataGridColumn = new DataGridTextColumn
					//	{
					//		Header = column.ColumnName,
					//		Binding = binding
					//	};

					//	var headerTemplate = new DataTemplate();
					//	var textBlockFactory = new FrameworkElementFactory(typeof(TextBlock));
					//	textBlockFactory.SetBinding(TextBlock.TextProperty, new Binding());
					//	headerTemplate.VisualTree = textBlockFactory;
					//	dataGridColumn.HeaderTemplate = headerTemplate;

					//	dataGrid.Columns.Add(dataGridColumn);
					//}

					//var rowNumberColumn = new DataGridTextColumn
					//{
					//	Header = "#",
					//	Binding = new Binding
					//	{
					//		Path = new PropertyPath("Items.IndexOf"),
					//		RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor) { AncestorType = typeof(DataGridRow) }
					//	}
					//};
					//dataGrid.Columns.Insert(0, rowNumberColumn);

					newLayoutContent.Content = tableViewControl;
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
					webView.KeyDown += async (s, e) =>
					{
						switch (e.Key)
						{
							case Key.F5:
								var editorText = await webView.GetEditorText();
								editorText = editorText.Replace("\n", "\r\n");
								var result = manager.Execute(editorText);
								if (!string.IsNullOrEmpty(result))
								{
									MessageBox.Show(result);
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
							var text = manager.GetObject(header);
							text = CreateProcedureRegex().Replace(text, "ALTER PROCEDURE");
							text = CreateFunctionRegex().Replace(text, "ALTER FUNCTION");
							await layoutDocumentPane.SetEditorText(header, text);
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

		public static async Task SetEditorText(this WebView2 webView, string text)
		{
			text = text.Replace("\r\n", "\\n").Replace("'", "\\'").Replace("\"", "\\\"");
			var script = $"setEditorText('{text}');";
			await webView.CoreWebView2.ExecuteScriptAsync(script);
		}

		[GeneratedRegex(@"\bcreate\s+procedure\b", RegexOptions.IgnoreCase)]
		private static partial Regex CreateProcedureRegex();
		[GeneratedRegex(@"\bcreate\s+function\b", RegexOptions.IgnoreCase)]
		private static partial Regex CreateFunctionRegex();
	}
}
