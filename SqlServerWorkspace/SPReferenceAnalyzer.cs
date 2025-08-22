using AvalonDock.Layout;

using Microsoft.Web.WebView2.Wpf;

using SqlServerWorkspace.Data;
using SqlServerWorkspace.DataModels;
using SqlServerWorkspace.Enums;
using SqlServerWorkspace.Extensions;

using System.Text.RegularExpressions;
using System.Windows;

namespace SqlServerWorkspace
{
	public static partial class SPReferenceAnalyzer
	{
		public static async Task AnalyzeAndAddReferences(this LayoutDocumentPane layoutDocumentPane, SqlManager manager, string procedureName)
		{
			try
			{
				var layoutContent = layoutDocumentPane.GetLayoutContent(procedureName);
				if (layoutContent?.Content is not WebView2 webView)
				{
					return;
				}

				string spText = await webView.GetEditorText();
				if (string.IsNullOrEmpty(spText))
				{
					return;
				}

				var references = AnalyzeSPReferences(manager, spText, procedureName);

				var spNode = FindProcedureNode(manager, procedureName);
				if (spNode == null)
				{
					return;
				}

				spNode.Children.Clear();

				AddReferenceNodesToTree(spNode, references);

				Common.Log($"Found {references.Tables.Count + references.Views.Count + references.Functions.Count + references.Procedures.Count} references in {procedureName}", LogType.Info);
			}
			catch (Exception ex)
			{
				Common.Log($"Error analyzing SP references: {ex.Message}", LogType.Error);
			}
		}

		private static SPReferences AnalyzeSPReferences(SqlManager manager, string spText, string currentProcedureName)
		{
			var references = new SPReferences();
			var allTables = manager.SelectTableNames();
			var allViews = manager.SelectViewNames();
			var allFunctions = manager.SelectFunctionNames();
			var allProcedures = manager.SelectProcedureNames();

			// 주석을 제거한 SQL 텍스트
			string cleanedSpText = RemoveComments(spText).ToLower();

			foreach (var table in allTables)
			{
				if (IsObjectReferenced(cleanedSpText, table.ToLower()))
				{
					references.Tables.Add(table);
				}
			}

			foreach (var view in allViews)
			{
				if (IsObjectReferenced(cleanedSpText, view.ToLower()))
				{
					references.Views.Add(view);
				}
			}

			foreach (var function in allFunctions)
			{
				if (IsFunctionReferenced(cleanedSpText, function.ToLower()))
				{
					references.Functions.Add(function);
				}
			}

			foreach (var procedure in allProcedures)
			{
				if (!procedure.Equals(currentProcedureName, StringComparison.OrdinalIgnoreCase) &&
					   IsProcedureReferenced(cleanedSpText, procedure.ToLower()))
				{
					references.Procedures.Add(procedure);
				}
			}

			return references;
		}

		private static string RemoveComments(string sqlText)
		{
			var stringLiterals = new List<string>();
			var stringPattern = @"'(?:[^']|'')*'";

			var textWithPlaceholders = Regex.Replace(sqlText, stringPattern, match =>
			{
				stringLiterals.Add(match.Value);
				return $"__STRING_LITERAL_{stringLiterals.Count - 1}__";
			});

			textWithPlaceholders = Regex.Replace(textWithPlaceholders, @"/\*.*?\*/", "", RegexOptions.Singleline);
			textWithPlaceholders = Regex.Replace(textWithPlaceholders, @"--.*?(?=\r?\n|$)", "", RegexOptions.Multiline);

			for (int i = 0; i < stringLiterals.Count; i++)
			{
				textWithPlaceholders = textWithPlaceholders.Replace($"__STRING_LITERAL_{i}__", stringLiterals[i]);
			}

			return textWithPlaceholders;
		}

		private static bool IsObjectReferenced(string spText, string objectName)
		{
			var patterns = new[]
			{
		$@"\bfrom\s+(?:\[?dbo\]?\.)?\[?{Regex.Escape(objectName)}\]?\b",
		$@"\bjoin\s+(?:\[?dbo\]?\.)?\[?{Regex.Escape(objectName)}\]?\b",
		$@"\binto\s+(?:\[?dbo\]?\.)?\[?{Regex.Escape(objectName)}\]?\b",
		$@"\bupdate\s+(?:\[?dbo\]?\.)?\[?{Regex.Escape(objectName)}\]?\b",
		$@"\bdelete\s+(?:\[?dbo\]?\.)?\[?{Regex.Escape(objectName)}\]?\b",
		$@"\binsert\s+into\s+(?:\[?dbo\]?\.)?\[?{Regex.Escape(objectName)}\]?\b"
	};
			return patterns.Any(pattern => Regex.IsMatch(spText, pattern, RegexOptions.IgnoreCase));
		}

		private static bool IsFunctionReferenced(string spText, string functionName)
		{
			var pattern = $@"(?:\[?dbo\]?\.)?\[?{Regex.Escape(functionName)}\]?\s*\(";
			return Regex.IsMatch(spText, pattern, RegexOptions.IgnoreCase);
		}

		private static bool IsProcedureReferenced(string spText, string procedureName)
		{
			var patterns = new[]
			{
		$@"\bexec(?:ute)?\s+(?:\[?dbo\]?\.)?\[?{Regex.Escape(procedureName)}\]?\b",
		$@"\bcall\s+(?:\[?dbo\]?\.)?\[?{Regex.Escape(procedureName)}\]?\b"
	};
			return patterns.Any(pattern => Regex.IsMatch(spText, pattern, RegexOptions.IgnoreCase));
		}

		private static TreeNode? FindProcedureNode(SqlManager manager, string procedureName)
		{
			foreach (var serverNode in manager.Nodes)
			{
				foreach (var databaseNode in serverNode.Children)
				{
					var procedureTitleNode = databaseNode.Children.FirstOrDefault(x => x.Type == TreeNodeType.ProcedureTitleNode);
					if (procedureTitleNode != null)
					{
						var spNode = procedureTitleNode.Children.FirstOrDefault(x => x.Name == procedureName);
						if (spNode != null)
						{
							return spNode;
						}
					}
				}
			}
			return null;
		}

		private static void AddReferenceNodesToTree(TreeNode spNode, SPReferences references)
		{
			foreach (var table in references.Tables)
			{
				var tableNode = new TreeNode(table, TreeNodeType.TableNode, spNode.Path.CombinePath(table), ResourceManager.TableIcon, ResourceManager.TableIconColor);
				spNode.Children.Add(tableNode);
			}

			foreach (var view in references.Views)
			{
				var viewNode = new TreeNode(view, TreeNodeType.ViewNode, spNode.Path.CombinePath(view), ResourceManager.ViewIcon, ResourceManager.ViewIconColor);
				spNode.Children.Add(viewNode);
			}

			foreach (var function in references.Functions)
			{
				var functionNode = new TreeNode(function, TreeNodeType.FunctionNode, spNode.Path.CombinePath(function), ResourceManager.FunctionIcon, ResourceManager.FunctionIconColor);
				spNode.Children.Add(functionNode);
			}

			foreach (var procedure in references.Procedures)
			{
				var procedureNode = new TreeNode(procedure, TreeNodeType.ProcedureNode, spNode.Path.CombinePath(procedure), ResourceManager.ProcedureIcon, ResourceManager.ProcedureIconColor);
				spNode.Children.Add(procedureNode);
			}

			Application.Current.Dispatcher.BeginInvoke(() =>
			{
				var mainWindow = (MainWindow)Application.Current.MainWindow;
				var treeView = mainWindow.DatabaseTreeView;

				var treeViewItem = treeView.GetTreeViewItemByNode(spNode);
				if (treeViewItem != null)
				{
					treeViewItem.IsExpanded = true;
					treeViewItem.UpdateLayout();
				}
			});
		}
	}

	public class SPReferences
	{
		public List<string> Tables { get; set; } = [];
		public List<string> Views { get; set; } = [];
		public List<string> Functions { get; set; } = [];
		public List<string> Procedures { get; set; } = [];

		public bool HasAnyReferences()
		{
			return Tables.Count != 0 || Views.Count != 0 || Functions.Count != 0 || Procedures.Count != 0;
		}
	}
}
