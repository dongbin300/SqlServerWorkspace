using Microsoft.VisualBasic.Devices;

using SqlServerWorkspace.DataModels;
using SqlServerWorkspace.Enums;
using SqlServerWorkspace.Views;

using System.Drawing;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace SqlServerWorkspace
{
	/// <summary>
	/// Interaction logic for MainWindow.xaml
	/// </summary>
	public partial class MainWindow : Window
	{
		private DispatcherTimer SaveSettingsTimer = new ();

		public MainWindow()
		{
			InitializeComponent();

			ResourceManager.Init();
			var windowPosition = ResourceManager.Settings.WindowPosition;
			Top = windowPosition.X == 0 ? Top : windowPosition.X;
			Left = windowPosition.Y == 0 ? Left : windowPosition.Y;
			Width = windowPosition.Width == 0 ? Width : windowPosition.Width;
			Height = windowPosition.Height == 0 ? Height : windowPosition.Height;

			Refresh();

			DataContext = this;
			DatabaseTreeView.ExpandAll(DatabaseTreeView);

			SaveSettingsTimer.Interval = TimeSpan.FromSeconds(3);
			SaveSettingsTimer.Tick += SaveSettingsTimer_Tick;
			SaveSettingsTimer.Start();
		}

		public void Refresh()
		{
			var keyword = DatabaseTreeViewFilterTextBox.Text;

			DatabaseTreeView.ItemsSource = DatabaseTreeViewFilterTextBox.Text.Length < 1 ?
				ResourceManager.ConnectionsNodes :
				Filter(ResourceManager.ConnectionsNodes, keyword);
		}

		private void SaveSettingsTimer_Tick(object? sender, EventArgs e)
		{
			ResourceManager.Settings.WindowPosition = new Rectangle((int)Top, (int)Left, (int)Width, (int)Height);
			ResourceManager.SaveSettings();
		}

		#region DATABASE TREEVIEW
		private IEnumerable<TreeNode> Filter(IEnumerable<IEnumerable<TreeNode>> items, string keyword)
		{
			List<TreeNode> filteredItems = [];
			foreach (var item in items)
			{
				foreach (var topNode in item)
				{
					var nodes = topNode.Search(keyword);
					filteredItems.AddRange(nodes);
				}
			}

			return filteredItems;
		}

		private void DatabaseTreeViewFilterTextBox_TextChanged(object sender, TextChangedEventArgs e)
		{
			Refresh();
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
			var view = new ConnectionView()
			{
				Owner = this
			};
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

		#region TREEVIEWITEM EVENT
		private void DatabaseTreeView_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
		{
			var dependencyObject = e.OriginalSource as DependencyObject;
			while (dependencyObject != null && dependencyObject is not TreeViewItem)
			{
				dependencyObject = VisualTreeHelper.GetParent(dependencyObject);
			}

			if (dependencyObject is not TreeViewItem treeViewItem)
			{
				return;
			}

			treeViewItem.IsSelected = true;
			switch (treeViewItem.DataContext)
			{
				case IEnumerable<TreeNode> nodes: // Server Node
					{
						// 추후에 추가
						//var contextMenu = new ContextMenu();

						//var menu1 = new MenuItem { Header = "Refresh" };
						//menu1.Click += MenuItem_Click;
						//contextMenu.Items.Add(menu1);

						//contextMenu.PlacementTarget = treeViewItem;
						//contextMenu.IsOpen = true;

						//contextMenu.Tag = treeViewItem;
					}
					break;

				case TreeNode node:
					{
						TreeViewContextMenu.MakeContextMenu(treeViewItem, node);
					}
					break;

				default:
					break;
			}
		}

		public void TreeViewMenuItem_Click(object sender, RoutedEventArgs e)
		{
			if (sender is not MenuItem menuItem)
			{
				return;
			}

			if (menuItem.Parent is not ContextMenu contextMenu)
			{
				return;
			}

			if (menuItem.Tag is not ContextMenuFunction function)
			{
				return;
			}

			if (contextMenu.Tag is not TreeViewItem treeViewItem)
			{
				return;
			}

			switch (treeViewItem.DataContext)
			{
				case TreeNode node:
					var manager = DatabaseTreeView.GetSqlManager(node);
					if (manager == null)
					{
						return;
					}

					switch (node.Type)
					{
						case TreeNodeType.DatabaseNode:
							TreeViewContextMenu.ProcessDatabaseNodeMenu(function, node);
							break;
						case TreeNodeType.TableTitleNode:
							TreeViewContextMenu.ProcessTableTitleNodeMenu(function, node, manager);
							break;
						case TreeNodeType.ViewTitleNode:
							TreeViewContextMenu.ProcessViewTitleNodeMenu(function, node, manager);
							break;
						case TreeNodeType.FunctionTitleNode:
							TreeViewContextMenu.ProcessFunctionTitleNodeMenu(function, node, manager);
							break;
						case TreeNodeType.ProcedureTitleNode:
							TreeViewContextMenu.ProcessProcedureTitleNodeMenu(function, node, manager);
							break;
						case TreeNodeType.TableNode:
							TreeViewContextMenu.ProcessTableNodeMenu(function, treeViewItem, node, manager);
							break;
						case TreeNodeType.ViewNode:
							TreeViewContextMenu.ProcessViewNodeMenu(function, treeViewItem, node, manager);
							break;
						case TreeNodeType.FunctionNode:
							TreeViewContextMenu.ProcessFunctionNodeMenu(function, treeViewItem, node, manager);
							break;
						case TreeNodeType.ProcedureNode:
							TreeViewContextMenu.ProcessProcedureNodeMenu(function, treeViewItem, node, manager);
							break;

						default:
							break;
					}
					break;

				case IEnumerable<TreeNode> nodes: // 추후
					break;

				default:
					break;
			}
		}
		#endregion

    }
}