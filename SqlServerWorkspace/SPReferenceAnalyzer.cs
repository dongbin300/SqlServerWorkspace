using AvalonDock.Layout;

using Microsoft.Web.WebView2.Wpf;

using SqlServerWorkspace.Data;
using SqlServerWorkspace.DataModels;
using SqlServerWorkspace.Enums;
using SqlServerWorkspace.Extensions;

using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;

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

				//Common.Log($"Found {references.Tables.Count + references.Views.Count + references.Functions.Count + references.Procedures.Count} references in {procedureName}", LogType.Info);
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
			try
			{
				var stringLiterals = new List<string>();
				var stringPattern = @"'(?:[^']|'')*'";

				var textWithPlaceholders = Regex.Replace(sqlText, stringPattern, match =>
				{
					stringLiterals.Add(match.Value);
					return $"__STRING_LITERAL_{stringLiterals.Count - 1}__";
				});

				// 블록 주석 제거 (/* ... */)
				textWithPlaceholders = Regex.Replace(textWithPlaceholders, @"/\*.*?\*/", " ", RegexOptions.Singleline);
				
				// 라인 주석 제거 (-- ...)
				textWithPlaceholders = Regex.Replace(textWithPlaceholders, @"--.*?(?=\r?\n|$)", " ", RegexOptions.Multiline);

				// 문자열 리터럴 복원
				for (int i = 0; i < stringLiterals.Count; i++)
				{
					textWithPlaceholders = textWithPlaceholders.Replace($"__STRING_LITERAL_{i}__", stringLiterals[i]);
				}

				// 여러 공백을 하나로 정규화
				textWithPlaceholders = Regex.Replace(textWithPlaceholders, @"\s+", " ");

				return textWithPlaceholders;
			}
			catch
			{
				// 오류 시 원본 반환
				return sqlText;
			}
		}

		private static bool IsObjectReferenced(string spText, string objectName)
		{
			var patterns = new[]
			{
				// 기본 패턴들 (더 유연하게)
				$@"\bfrom\s+(?:\[?dbo\]?\.)?\[?{Regex.Escape(objectName)}\]?\b",
				$@"\bjoin\s+(?:\[?dbo\]?\.)?\[?{Regex.Escape(objectName)}\]?\b",
				$@"\binto\s+(?:\[?dbo\]?\.)?\[?{Regex.Escape(objectName)}\]?\b",
				$@"\bupdate\s+(?:\[?dbo\]?\.)?\[?{Regex.Escape(objectName)}\]?\b",
				$@"\bdelete\s+from\s+(?:\[?dbo\]?\.)?\[?{Regex.Escape(objectName)}\]?\b",
				$@"\bdelete\s+(?:\[?dbo\]?\.)?\[?{Regex.Escape(objectName)}\]?\b",
				$@"\binsert\s+into\s+(?:\[?dbo\]?\.)?\[?{Regex.Escape(objectName)}\]?\b",
				
				// JOIN 패턴들
				$@"\binner\s+join\s+(?:\[?dbo\]?\.)?\[?{Regex.Escape(objectName)}\]?\b",
				$@"\bleft\s+(?:outer\s+)?join\s+(?:\[?dbo\]?\.)?\[?{Regex.Escape(objectName)}\]?\b",
				$@"\bright\s+(?:outer\s+)?join\s+(?:\[?dbo\]?\.)?\[?{Regex.Escape(objectName)}\]?\b",
				$@"\bfull\s+(?:outer\s+)?join\s+(?:\[?dbo\]?\.)?\[?{Regex.Escape(objectName)}\]?\b",
				$@"\bcross\s+join\s+(?:\[?dbo\]?\.)?\[?{Regex.Escape(objectName)}\]?\b",
				
				// MERGE 문 패턴들
				$@"\bmerge\s+(?:\[?dbo\]?\.)?\[?{Regex.Escape(objectName)}\]?\b",
				$@"\bmerge\s+(?:\[?dbo\]?\.)?\[?{Regex.Escape(objectName)}\]?\s+(?:as\s+)?\w+",
				$@"\busing\s+(?:\[?dbo\]?\.)?\[?{Regex.Escape(objectName)}\]?\b",
				$@"\busing\s+\([^)]*\bfrom\s+(?:\[?dbo\]?\.)?\[?{Regex.Escape(objectName)}\]?",
				
				// 서브쿼리 패턴들
				$@"\bexists\s*\([^)]*\bfrom\s+(?:\[?dbo\]?\.)?\[?{Regex.Escape(objectName)}\]?",
				$@"\bin\s*\([^)]*\bfrom\s+(?:\[?dbo\]?\.)?\[?{Regex.Escape(objectName)}\]?",
				$@"\bnot\s+in\s*\([^)]*\bfrom\s+(?:\[?dbo\]?\.)?\[?{Regex.Escape(objectName)}\]?",
				$@"\bnot\s+exists\s*\([^)]*\bfrom\s+(?:\[?dbo\]?\.)?\[?{Regex.Escape(objectName)}\]?",
				
				// CTE 패턴들
				$@"\bwith\s+(?:\w+\s+as\s+\([^)]*\),?\s*)*\w*\s*as\s*\([^)]*\bfrom\s+(?:\[?dbo\]?\.)?\[?{Regex.Escape(objectName)}\]?",
				
				// 테이블 별칭이 있는 경우 (더 유연하게)
				$@"\bfrom\s+(?:\[?dbo\]?\.)?\[?{Regex.Escape(objectName)}\]?\s+(?:as\s+)?\w+",
				$@"\bjoin\s+(?:\[?dbo\]?\.)?\[?{Regex.Escape(objectName)}\]?\s+(?:as\s+)?\w+",
				
				// 단순히 테이블명만 언급되는 경우 (공백, 줄바꿈 허용)
				$@"\s(?:\[?dbo\]?\.)?\[?{Regex.Escape(objectName)}\]?\s",
				
				// FROM 뒤에 여러 공백/줄바꿈이 있는 경우
				$@"\bfrom\s+(?:\s|\r|\n)*(?:\[?dbo\]?\.)?\[?{Regex.Escape(objectName)}\]?",
				
				// 쉼표로 구분된 여러 테이블
				$@",\s*(?:\[?dbo\]?\.)?\[?{Regex.Escape(objectName)}\]?\b",
				$@"\bfrom\s+[^,]*,\s*(?:\[?dbo\]?\.)?\[?{Regex.Escape(objectName)}\]?",
				
				// 매우 깨진 SQL을 위한 단순 패턴들
				$@"\bfrom\s*(?:\r|\n|\s)*(?:\[?dbo\]?\.)?\[?{Regex.Escape(objectName)}\]?",
				$@"(?:\r|\n|\s)+(?:\[?dbo\]?\.)?\[?{Regex.Escape(objectName)}\]?(?:\r|\n|\s)+",
				
				// 단어 경계와 공백만으로 감지 (최후 수단)
				$@"(?:^|\s)(?:\[?dbo\]?\.)?\[?{Regex.Escape(objectName)}\]?(?:\s|$)",
				
				// fr으로 시작하는 깨진 from 절
				$@"\bfr[a-z]*\s*(?:\[?dbo\]?\.)?\[?{Regex.Escape(objectName)}\]?",
				
				// om으로 끝나는 깨진 from 절 
				$@"[a-z]*om\s*(?:\[?dbo\]?\.)?\[?{Regex.Escape(objectName)}\]?"
			};
			
			// 기본 패턴들을 먼저 체크
			var basicPatterns = patterns.Take(patterns.Length - 5).ToArray();
			if (basicPatterns.Any(pattern => Regex.IsMatch(spText, pattern, RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.Singleline)))
			{
				return true;
			}
			
			// 단순 패턴들로 추가 체크 (노이즈 줄이기 위해 길이 체크)
			if (objectName.Length >= 3)
			{
				var fallbackPatterns = patterns.Skip(patterns.Length - 5).ToArray();
				return fallbackPatterns.Any(pattern => Regex.IsMatch(spText, pattern, RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.Singleline));
			}
			
			return false;
		}

		private static bool IsFunctionReferenced(string spText, string functionName)
		{
			var patterns = new[]
			{
				// 함수 호출 패턴들
				$@"(?:\[?dbo\]?\.)?\[?{Regex.Escape(functionName)}\]?\s*\(",
				$@"\bselect\s+.*?(?:\[?dbo\]?\.)?\[?{Regex.Escape(functionName)}\]?\s*\(",
				$@"\bfrom\s+(?:\[?dbo\]?\.)?\[?{Regex.Escape(functionName)}\]?\s*\(",
				$@"\bjoin\s+(?:\[?dbo\]?\.)?\[?{Regex.Escape(functionName)}\]?\s*\(",
				// 테이블 값 함수
				$@"\bfrom\s+(?:\[?dbo\]?\.)?\[?{Regex.Escape(functionName)}\]?\s*\([^)]*\)",
				$@"\bjoin\s+(?:\[?dbo\]?\.)?\[?{Regex.Escape(functionName)}\]?\s*\([^)]*\)"
			};
			return patterns.Any(pattern => Regex.IsMatch(spText, pattern, RegexOptions.IgnoreCase | RegexOptions.Multiline));
		}

		private static bool IsProcedureReferenced(string spText, string procedureName)
		{
			var patterns = new[]
			{
				$@"\bexec(?:ute)?\s+(?:\[?dbo\]?\.)?\[?{Regex.Escape(procedureName)}\]?\b",
				$@"\bcall\s+(?:\[?dbo\]?\.)?\[?{Regex.Escape(procedureName)}\]?\b",
				// 괄호가 있는 경우
				$@"\bexec(?:ute)?\s+(?:\[?dbo\]?\.)?\[?{Regex.Escape(procedureName)}\]?\s*\(",
				// sp_executesql에서 실행되는 경우
				$@"sp_executesql\s+.*?(?:\[?dbo\]?\.)?\[?{Regex.Escape(procedureName)}\]?"
			};
			return patterns.Any(pattern => Regex.IsMatch(spText, pattern, RegexOptions.IgnoreCase | RegexOptions.Multiline));
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

			// UI 업데이트를 즉시 실행하고 TreeView를 새로고침
			Application.Current.Dispatcher.Invoke(() =>
			{
				var mainWindow = (MainWindow)Application.Current.MainWindow;
				var treeView = mainWindow.DatabaseTreeView;

				// 필터링된 상태인지 확인
				bool isFiltered = mainWindow.IsFiltered;

				if (isFiltered)
				{
					// 깜빡임 방지를 위해 UI 업데이트를 한 번에 처리
					Application.Current.Dispatcher.BeginInvoke(
						System.Windows.Threading.DispatcherPriority.Background,
						new Action(() =>
						{
							// 필터링된 상태면 전체 트리를 다시 필터링하여 새로운 참조들도 포함시킴
							mainWindow.Refresh();
							
							// 새로고침 직후 expand 처리
							Application.Current.Dispatcher.BeginInvoke(
								System.Windows.Threading.DispatcherPriority.Render,
								new Action(() =>
								{
									var refreshedTreeViewItem = treeView.GetTreeViewItemByNode(spNode);
									if (refreshedTreeViewItem != null)
									{
										refreshedTreeViewItem.IsExpanded = true;
									}
								}));
						}));
				}
				else
				{
					// TreeView 데이터 소스가 바뀌었음을 알림
					treeView.Items.Refresh();

					var treeViewItem = treeView.GetTreeViewItemByNode(spNode);
					if (treeViewItem != null)
					{
						// TreeViewItem의 HasItems 속성을 업데이트하기 위해 Items를 새로고침
						treeViewItem.Items.Refresh();
						
						// 즉시 레이아웃 업데이트
						treeViewItem.UpdateLayout();
						treeView.UpdateLayout();
						
						// 렌더링 우선순위로 expand 처리
						Application.Current.Dispatcher.BeginInvoke(
							System.Windows.Threading.DispatcherPriority.Render,
							new Action(() =>
							{
								// expand icon이 보이도록 강제로 HasItems 상태 갱신
								treeViewItem.InvalidateProperty(ItemsControl.HasItemsProperty);
								treeViewItem.IsExpanded = true;
								treeViewItem.UpdateLayout();
							}));
							
						// 최종 정리 작업
						Application.Current.Dispatcher.BeginInvoke(
							System.Windows.Threading.DispatcherPriority.Background,
							new Action(() =>
							{
								treeView.UpdateLayout();
								// 시각적 업데이트 강제 실행
								treeViewItem.InvalidateVisual();
								treeViewItem.InvalidateArrange();
							}));
					}
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
