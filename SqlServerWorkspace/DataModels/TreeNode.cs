namespace SqlServerWorkspace.DataModels
{
	public class TreeNode(string name)
	{
		public string Name { get; set; } = name;
		public List<TreeNode> Children { get; set; } = [];
	}
}
