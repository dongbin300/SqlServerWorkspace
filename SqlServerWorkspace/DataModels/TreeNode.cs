using SqlServerWorkspace.Enums;

using System.Windows.Media.Imaging;

namespace SqlServerWorkspace.DataModels
{
	public class TreeNode(string name, TreeNodeType type, string path, BitmapImage? iconSource = null)
	{
		public string Name { get; set; } = name;
		public TreeNodeType Type { get; set; } = type;
		public List<TreeNode> Children { get; set; } = [];
		public string Path { get; set; } = path;
		public BitmapImage Icon { get; set; } = iconSource ?? default!;

		public string GetParentName()
		{
			var parts = Path.Split('/', StringSplitOptions.RemoveEmptyEntries);
			return parts.Length < 2 ? string.Empty : parts[^2];
		}

		public List<TreeNode> Search(string keyword)
		{
			List<TreeNode> results = [];

			bool ContainsKeyword(TreeNode node) =>
				node.Name.Contains(keyword, StringComparison.OrdinalIgnoreCase) &&
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

			TreeNode CloneNode(TreeNode node)
			{
				return new TreeNode(node.Name, node.Type, node.Path, node.Icon);
			}

			void SearchInternal(TreeNode sourceNode, TreeNode targetNode)
			{
				foreach (var child in sourceNode.Children)
				{
					if (ContainsKeyword(child) || IsAlwaysIncluded(child))
					{
						var clonedChild = CloneNode(child);
						targetNode.Children.Add(clonedChild);
						SearchInternal(child, clonedChild);
					}
					else
					{
						var matchedChild = new TreeNode(child.Name, child.Type, child.Path, child.Icon);
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
