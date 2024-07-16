using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;

namespace TreeViewTest
{
	/// <summary>
	/// Interaction logic for MainWindow.xaml
	/// </summary>
	public partial class MainWindow : Window
	{
		public List<List<TreeNode>> RootTreeNodes { get; set; }
		public List<TreeNode> TreeNodes { get; set; }

		public MainWindow()
		{
			InitializeComponent();

			TreeNodes =
			[
				new TreeNode("Rooooooooooooooooooooooot")
				{
					Children =                  
					[
						new TreeNode("Child 1")
						{
							Children =
							[
								new TreeNode("Item 1"),
								new TreeNode("Item 2"),
								new TreeNode("Item 3")
							]
						},
						new TreeNode("Child 2"),
						new TreeNode("Child 3"),
						new TreeNode("Child 4")
					]
				}
			];
			RootTreeNodes = new List<List<TreeNode>>();
			RootTreeNodes.Add(TreeNodes);

			treeView.ItemsSource = RootTreeNodes;
		}

		private void Border_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
		{
			if (sender is Border border && border.TemplatedParent is TreeViewItem item)
			{
				if (item.HasItems)
				{
					item.IsExpanded = !item.IsExpanded;
				}
				e.Handled = true;
			}
		}
	}
}