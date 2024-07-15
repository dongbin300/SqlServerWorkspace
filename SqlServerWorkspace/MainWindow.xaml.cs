using SqlServerWorkspace.DataModels;
using SqlServerWorkspace.Enums;
using SqlServerWorkspace.Views;

using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Input;

namespace SqlServerWorkspace
{
	/// <summary>
	/// Interaction logic for MainWindow.xaml
	/// </summary>
	public partial class MainWindow : Window
	{
		public static readonly DependencyProperty StatusTextProperty = DependencyProperty.Register("StatusText", typeof(string), typeof(MainWindow), new PropertyMetadata(""));
		private TreeViewItem currentSelectedItem = default!;
		private TreeNode currentSelectedNode = default!;

		public string StatusText
		{
			get { return (string)GetValue(StatusTextProperty); }
			set { SetValue(StatusTextProperty, value); }
		}

		public MainWindow()
		{
			InitializeComponent();

			ResourceManager.Init();
			Refresh();

			DataContext = this;
		}

		public void Refresh()
		{
			DatabaseTreeView.ItemsSource = ResourceManager.Connections.Select(x => x.Nodes);
		}

		private void DatabaseTreeViewItem_Loaded(object sender, RoutedEventArgs e)
		{
			DatabaseTreeView.ExpandAll(DatabaseTreeView);
		}

		private void DatabaseTreeViewItem_Selected(object sender, RoutedEventArgs e)
		{
			if (e.OriginalSource is not TreeViewItem selectedItem)
			{
				return;
			}

			currentSelectedItem = selectedItem;

			/* Prevent event bubbling */
			e.Handled = true;
			var parent = VisualTreeHelper.GetParent(selectedItem);
			while (parent != null && parent is TreeViewItem item)
			{
				var parentItem = item;
				parentItem.IsSelected = false;
				parent = VisualTreeHelper.GetParent(parent);
			}

			switch (selectedItem.DataContext)
			{
				case TreeNode selectedNode:
					switch (selectedNode.Type)
					{
						case TreeNodeType.DatabaseNode:
						case TreeNodeType.TableTitleNode:
						case TreeNodeType.ViewTitleNode:
						case TreeNodeType.FunctionTitleNode:
						case TreeNodeType.ProcedureTitleNode:
							selectedItem.IsExpanded = !selectedItem.IsExpanded;
							break;

						default:
							break;
					}

					selectedItem.IsSelected = false;
					break;

				case IEnumerable<TreeNode> selectedNode:
					switch (selectedNode.First().Type)
					{
						case TreeNodeType.ServerNode:
							selectedItem.IsExpanded = !selectedItem.IsExpanded;
							break;

						default:
							break;
					}

					selectedItem.IsSelected = false;
					break;

				default:
					break;
			}
		}

		private async void DatabaseTreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
		{
			if (e.NewValue is not TreeNode selectedNode)
			{
				return;
			}

			currentSelectedNode = selectedNode;

			var manager = DatabaseTreeView.GetSqlManager(selectedNode);
			if (manager == null)
			{
				return;
			}

			switch (selectedNode.Type)
			{
				case TreeNodeType.TableNode:
				case TreeNodeType.ViewNode:
				case TreeNodeType.FunctionNode:
				case TreeNodeType.ProcedureNode:
					await EntryPane.CreateNewOrOpenTab(manager, selectedNode);
					break;

				case TreeNodeType.DatabaseNode:
					if (selectedNode.Children.Count == 0)
					{
						TreeViewManager.MakeDatabaseTree(selectedNode);
						Refresh();
					}
					break;

				case TreeNodeType.TableTitleNode:
					if (selectedNode.Children.Count == 0)
					{
						TreeViewManager.MakeTableTree(manager, selectedNode);
						Refresh();
					}
					break;

				case TreeNodeType.ViewTitleNode:
					if (selectedNode.Children.Count == 0)
					{
						TreeViewManager.MakeViewTree(manager, selectedNode);
						Refresh();
					}
					break;

				case TreeNodeType.FunctionTitleNode:
					if (selectedNode.Children.Count == 0)
					{
						TreeViewManager.MakeFunctionTree(manager, selectedNode);
						Refresh();
					}
					break;

				case TreeNodeType.ProcedureTitleNode:
					if (selectedNode.Children.Count == 0)
					{
						TreeViewManager.MakeProcedureTree(manager, selectedNode);
						Refresh();
					}
					break;

				default:
					break;
			}
		}

		private void Connect_Click(object sender, RoutedEventArgs e)
		{
			var view = new ConnectionView();
			if (view.ShowDialog() ?? false)
			{
				Refresh();
			}
		}

		private void Exit_Click(object sender, RoutedEventArgs e)
		{
			Environment.Exit(0);
		}

		private void DatabaseTreeViewFilterTextBox_TextChanged(object sender, TextChangedEventArgs e)
		{
			var keyword = DatabaseTreeViewFilterTextBox.Text;

			if (keyword.Length < 1)
			{
				Refresh();
				return;
			}

			var items = ResourceManager.Connections.Select(x => x.Nodes);

			List<TreeNode> filteredItems = [];
			foreach (var item in items)
			{
				foreach (var topNode in item)
				{
					var nodes = topNode.Search(keyword);
					filteredItems.AddRange(nodes);
				}
			}

			DatabaseTreeView.ItemsSource = filteredItems;
		}
	}
}