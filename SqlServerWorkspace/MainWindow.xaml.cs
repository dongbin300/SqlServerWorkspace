using SqlServerWorkspace.DataModels;
using SqlServerWorkspace.Enums;
using SqlServerWorkspace.Views;

using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace SqlServerWorkspace
{
	/// <summary>
	/// Interaction logic for MainWindow.xaml
	/// </summary>
	public partial class MainWindow : Window
	{
		public static readonly DependencyProperty StatusTextProperty = DependencyProperty.Register("StatusText", typeof(string), typeof(MainWindow), new PropertyMetadata(""));

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
			DatabaseTreeView.ExpandAll(DatabaseTreeView);
		}

		public void Refresh()
		{
			DatabaseTreeView.ItemsSource = ResourceManager.Connections.Select(x => x.Nodes);
		}

		#region DATABASE TREEVIEW
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

		private async void Border_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
		{
			if (sender is Border border && border.TemplatedParent is TreeViewItem item)
			{
				switch (item.DataContext)
				{
					case IEnumerable<TreeNode> nodes: // Server Node
						break;

					case TreeNode node:
						var manager = DatabaseTreeView.GetSqlManager(node);
						if (manager == null)
						{
							return;
						}

						switch (node.Type)
						{
							case TreeNodeType.TableNode:
							case TreeNodeType.ViewNode:
							case TreeNodeType.FunctionNode:
							case TreeNodeType.ProcedureNode:
								await EntryPane.CreateNewOrOpenTab(manager, node);
								break;

							case TreeNodeType.DatabaseNode:
								if (node.Children.Count == 0)
								{
									TreeViewManager.MakeDatabaseTree(node);
									Refresh();
								}
								break;

							case TreeNodeType.TableTitleNode:
								if (node.Children.Count == 0)
								{
									TreeViewManager.MakeTableTree(manager, node);
									Refresh();
								}
								break;

							case TreeNodeType.ViewTitleNode:
								if (node.Children.Count == 0)
								{
									TreeViewManager.MakeViewTree(manager, node);
									Refresh();
								}
								break;

							case TreeNodeType.FunctionTitleNode:
								if (node.Children.Count == 0)
								{
									TreeViewManager.MakeFunctionTree(manager, node);
									Refresh();
								}
								break;

							case TreeNodeType.ProcedureTitleNode:
								if (node.Children.Count == 0)
								{
									TreeViewManager.MakeProcedureTree(manager, node);
									Refresh();
								}
								break;

							default:
								break;
						}
						break;

					default:
						break;
				}

				if (item.HasItems)
				{
					item.IsExpanded = !item.IsExpanded;
				}

				e.Handled = true;
			}
		}
		#endregion

		#region MENU
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
		#endregion
	}
}