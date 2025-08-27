using SqlServerWorkspace.Enums;

namespace SqlServerWorkspace.DataModels
{
	public class TreeNode(string name, TreeNodeType type, string path, string? svgData = null, string? svgColor = null, bool isExpanded = false)
	{
		public string Name { get; set; } = name;
		public TreeNodeType Type { get; set; } = type;
		public List<TreeNode> Children { get; set; } = [];
		public string Path { get; set; } = path;
		public string SvgData { get; set; } = svgData ?? string.Empty;
		public string SvgColor { get; set; } = svgColor ?? string.Empty;
		public bool IsExpanded { get; set; } = isExpanded;

		public string GetParentName()
		{
			var parts = Path.Split('/', StringSplitOptions.RemoveEmptyEntries);
			return parts.Length < 2 ? string.Empty : parts[^2];
		}

		public string GetDatabaseName()
		{
			var parts = Path.Split('/', StringSplitOptions.RemoveEmptyEntries);
			return parts.Length < 2 ? string.Empty : parts[1];
		}

		/// <summary>
		/// ";" 구분자를 이용하여 다중 검색 지원
		/// </summary>
		/// <param name="keyword"></param>
		/// <returns></returns>
		public List<TreeNode> Search(string keyword)
		{
			List<TreeNode> results = [];

			var keywords = keyword.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

			bool ContainsKeyword(TreeNode node) =>
				keywords.Any(k => node.Name.Contains(k, StringComparison.OrdinalIgnoreCase)) &&
				(node.Type == TreeNodeType.TableNode ||
				node.Type == TreeNodeType.ViewNode ||
				node.Type == TreeNodeType.FunctionNode ||
				node.Type == TreeNodeType.ProcedureNode);

			bool IsAlwaysIncluded(TreeNode node) =>
				node.Type == TreeNodeType.ServerNode ||
				node.Type == TreeNodeType.DatabaseNode ||
				node.Type == TreeNodeType.TableTitleNode ||
				node.Type == TreeNodeType.ViewTitleNode ||
				node.Type == TreeNodeType.FunctionTitleNode ||
				node.Type == TreeNodeType.ProcedureTitleNode;

			bool IsReferenceNode(TreeNode node, TreeNode parent)
			{
				// 프로시저 노드의 직접 자식들은 참조 항목으로 간주
				// 단, 부모 프로시저가 키워드에 매치되어야 함
				return parent.Type == TreeNodeType.ProcedureNode &&
					   (node.Type == TreeNodeType.TableNode ||
						node.Type == TreeNodeType.ViewNode ||
						node.Type == TreeNodeType.FunctionNode ||
						node.Type == TreeNodeType.ProcedureNode) &&
					   ContainsKeyword(parent);
			}

			TreeNode CloneNode(TreeNode node)
			{
				return new TreeNode(node.Name, node.Type, node.Path, node.SvgData, node.SvgColor, node.IsExpanded);
			}

			void SearchInternal(TreeNode sourceNode, TreeNode targetNode)
			{
				foreach (var child in sourceNode.Children)
				{
					if (ContainsKeyword(child) || IsAlwaysIncluded(child) || IsReferenceNode(child, sourceNode))
					{
						var clonedChild = CloneNode(child);
						targetNode.Children.Add(clonedChild);
						SearchInternal(child, clonedChild);
					}
					else
					{
						var matchedChild = new TreeNode(child.Name, child.Type, child.Path, child.SvgData, child.SvgColor, child.IsExpanded);
						SearchInternal(child, matchedChild);
						if (matchedChild.Children.Count > 0)
						{
							targetNode.Children.Add(matchedChild);
						}
					}
				}
			}

			var rootClone = CloneNode(this);
			SearchInternal(this, rootClone);
			results.Add(rootClone);
			return results;
		}
	}
}
