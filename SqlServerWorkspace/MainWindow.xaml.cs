using AvalonDock.Layout;

using SqlServerWorkspace.Data;
using SqlServerWorkspace.DataModels;
using SqlServerWorkspace.Enums;
using SqlServerWorkspace.Views;

using System.Drawing;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
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
		private DispatcherTimer SaveSettingsTimer = new();
		private List<string> FilterKeywords = new List<string>();

		public bool IsFiltered => FilterKeywords.Count > 0;

		public MainWindow()
		{
			InitializeComponent();

			// 버전 정보를 가져와서 창 제목에 추가
			var version = Assembly.GetExecutingAssembly().GetName().Version;
			Title = $"SQL Server Workspace v{version}";

			ResourceManager.Init();
			var windowPosition = ResourceManager.Settings.WindowPosition;

			// 윈도우 상태 및 위치 복원
			if (windowPosition.Width > 0 && windowPosition.Height > 0)
			{
				var screenWidth = SystemParameters.PrimaryScreenWidth;
				var screenHeight = SystemParameters.PrimaryScreenHeight;

				// 최종 적용될 크기 계산 (최소 크기 보장)
				var finalWidth = Math.Max(windowPosition.Width, 800);
				var finalHeight = Math.Max(windowPosition.Height, 600);

				// 저장된 위치 그대로 복원
				var restoredLeft = windowPosition.X;
				var restoredTop = windowPosition.Y;

				// 화면 범위 확인 (최종 크기 기준)
				if (restoredLeft < 0 || restoredLeft + finalWidth > screenWidth)
					restoredLeft = (int)((screenWidth - finalWidth) / 2);
				if (restoredTop < 0 || restoredTop + finalHeight > screenHeight)
					restoredTop = (int)((screenHeight - finalHeight) / 2);

				// 음수 좌표 방지
				restoredLeft = Math.Max(0, restoredLeft);
				restoredTop = Math.Max(0, restoredTop);

				// 위치 및 크기 적용
				Left = restoredLeft;
				Top = restoredTop;
				Width = finalWidth;
				Height = finalHeight;

				// 창 상태 복원
				if (Enum.TryParse<WindowState>(ResourceManager.Settings.WindowState, out var savedState))
				{
					// 최소화 상태는 복원하지 않음
					WindowState = savedState == WindowState.Minimized ? WindowState.Normal : savedState;
				}
			}

			// TreeView 성능 최적화 적용
			DatabaseTreeView.OptimizeTreeView();

			// 비동기로 데이터 로드
			Task.Run(async () =>
			{
				await Application.Current.Dispatcher.InvokeAsync(async () =>
				{
					await RefreshAsync();
				});
			});

			DataContext = this;

			SaveSettingsTimer.Interval = TimeSpan.FromSeconds(3);
			SaveSettingsTimer.Tick += SaveSettingsTimer_Tick;
			SaveSettingsTimer.Start();
		}

		public void Refresh(bool isTitleExpand = false)
		{
			// 현재 확장된 아이템들의 경로를 저장
			var expandedPaths = new HashSet<string>();
			if (DatabaseTreeView.ItemsSource != null)
			{
				SaveExpandedState(DatabaseTreeView, expandedPaths);
			}

			DatabaseTreeView.ItemsSource = isTitleExpand || FilterKeywords.Count == 0 ?
				ResourceManager.ConnectionsNodes :
				FilterWithKeywords(ResourceManager.ConnectionsNodes, FilterKeywords);

			// 확장 상태를 복원
			if (expandedPaths.Count > 0)
			{
				Application.Current.Dispatcher.BeginInvoke(new Action(() =>
				{
					RestoreExpandedState(DatabaseTreeView, expandedPaths);
				}), DispatcherPriority.Background);
			}
		}

		public async Task RefreshAsync(bool isTitleExpand = false)
		{
			// 현재 확장된 아이템들의 경로를 저장
			var expandedPaths = new HashSet<string>();
			if (DatabaseTreeView.ItemsSource != null)
			{
				SaveExpandedState(DatabaseTreeView, expandedPaths);
			}

			// 데이터 소스 설정
			DatabaseTreeView.ItemsSource = isTitleExpand || FilterKeywords.Count == 0 ?
				ResourceManager.ConnectionsNodes :
				FilterWithKeywords(ResourceManager.ConnectionsNodes, FilterKeywords);

			// 비동기로 확장 상태 복원
			if (expandedPaths.Count > 0)
			{
				await Task.Run(() =>
				{
					Application.Current.Dispatcher.BeginInvoke(new Action(() =>
					{
						RestoreExpandedState(DatabaseTreeView, expandedPaths);
					}), DispatcherPriority.Background);
				});
			}

			// 정기적으로 만료된 캐시 항목 정리
			DatabaseCache.CleanExpiredItems();
		}

		private void SaveExpandedState(ItemsControl itemsControl, HashSet<string> expandedPaths)
		{
			for (int i = 0; i < itemsControl.Items.Count; i++)
			{
				var container = itemsControl.ItemContainerGenerator.ContainerFromIndex(i) as TreeViewItem;
				if (container?.IsExpanded == true && container.Tag is string path)
				{
					expandedPaths.Add(path);
				}
				
				if (container != null)
				{
					SaveExpandedState(container, expandedPaths);
				}
			}
		}

		private void RestoreExpandedState(ItemsControl itemsControl, HashSet<string> expandedPaths)
		{
			for (int i = 0; i < itemsControl.Items.Count; i++)
			{
				var container = itemsControl.ItemContainerGenerator.ContainerFromIndex(i) as TreeViewItem;
				if (container?.Tag is string path && expandedPaths.Contains(path))
				{
					container.IsExpanded = true;
					container.UpdateLayout();
				}
				
				if (container != null)
				{
					RestoreExpandedState(container, expandedPaths);
				}
			}
		}

		private void SaveSettingsTimer_Tick(object? sender, EventArgs e)
		{
			// 창 위치 및 상태 저장
			ResourceManager.Settings.WindowPosition = new Rectangle((int)Left, (int)Top, (int)Width, (int)Height);
			ResourceManager.Settings.WindowState = WindowState.ToString();
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

		private IEnumerable<TreeNode> FilterWithKeywords(IEnumerable<IEnumerable<TreeNode>> items, List<string> keywords)
		{
			List<TreeNode> filteredItems = [];
			foreach (var item in items)
			{
				foreach (var topNode in item)
				{
					var combinedKeyword = string.Join(";", keywords);
					var nodes = topNode.Search(combinedKeyword);
					filteredItems.AddRange(nodes);
				}
			}

			return filteredItems;
		}

		private void AddKeyword(string keyword)
		{
			if (string.IsNullOrWhiteSpace(keyword) || FilterKeywords.Contains(keyword))
				return;

			FilterKeywords.Add(keyword);
			CreateKeywordCard(keyword);
			Refresh();
		}

		private void RemoveKeyword(string keyword)
		{
			FilterKeywords.Remove(keyword);
			RemoveKeywordCard(keyword);
			Refresh();
		}


		private void CreateKeywordCard(string keyword)
		{
			var border = new Border
			{
				Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0xCC, 0x96, 0xF8)),
				CornerRadius = new CornerRadius(4),
				Margin = new Thickness(2),
				Padding = new Thickness(8, 4, 8, 4),
				Cursor = Cursors.Hand,
				Tag = keyword
			};

			var textBlock = new TextBlock
			{
				Text = keyword,
				Foreground = Brushes.White,
				FontSize = 12,
				VerticalAlignment = VerticalAlignment.Center
			};

			border.Child = textBlock;
			border.MouseLeftButtonDown += KeywordCard_MouseLeftButtonDown;

			KeywordCardsPanel.Children.Add(border);
		}

		private void RemoveKeywordCard(string keyword)
		{
			var cardToRemove = KeywordCardsPanel.Children
				.OfType<Border>()
				.FirstOrDefault(b => b.Tag?.ToString() == keyword);

			if (cardToRemove != null)
			{
				KeywordCardsPanel.Children.Remove(cardToRemove);
			}
		}

		private void KeywordCard_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
		{
			if (sender is Border border && border.Tag is string keyword)
			{
				RemoveKeyword(keyword);
			}
		}

		private void DatabaseTreeViewFilterTextBox_KeyDown(object sender, KeyEventArgs e)
		{
			if (e.Key == Key.Enter)
			{
				var keyword = DatabaseTreeViewFilterTextBox.Text.Trim();
				if (!string.IsNullOrEmpty(keyword))
				{
					AddKeyword(keyword);
					DatabaseTreeViewFilterTextBox.Text = "";
				}
			}
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
								await EntryDocumentPane.CreateNewOrOpenTab(manager, node);
								break;
								
							case TreeNodeType.ProcedureNode:
								await EntryDocumentPane.CreateNewOrOpenTab(manager, node);
								
								// 이미 참조 항목이 있는지 확인
								bool hasReferences = node.Children.Count > 0;
								
								if (hasReferences)
								{
									// 참조 항목이 있으면 expand 상태를 토글
									// UI에서 실제 expand 상태를 확인
									await Application.Current.Dispatcher.BeginInvoke(new Action(() =>
									{
										bool currentExpandState = item.IsExpanded;
										item.IsExpanded = !currentExpandState;
										item.UpdateLayout();
										
										// 확실한 토글을 위해 한 번 더 체크
										Application.Current.Dispatcher.BeginInvoke(new Action(() =>
										{
											if (item.IsExpanded != !currentExpandState)
											{
												item.IsExpanded = !currentExpandState;
											}
										}), DispatcherPriority.Background);
									}), DispatcherPriority.Render);
								}
								else
								{
									// 참조 항목이 없으면 분석 후 expand
									await EntryDocumentPane.AnalyzeAndAddReferences(manager, node.Name);
									item.IsExpanded = true;
								}
								break;

							case TreeNodeType.DatabaseNode:
								if (node.Children.Count == 0)
								{
									TreeViewManager.MakeDatabaseTree(node);
									Refresh(true);
								}
								break;

							case TreeNodeType.TableTitleNode:
								if (node.Children.Count == 0)
								{
									TreeViewManager.MakeTableTree(manager, node);
									Refresh(true);
								}
								break;

							case TreeNodeType.ViewTitleNode:
								if (node.Children.Count == 0)
								{
									TreeViewManager.MakeViewTree(manager, node);
									Refresh(true);
								}
								break;

							case TreeNodeType.FunctionTitleNode:
								if (node.Children.Count == 0)
								{
									TreeViewManager.MakeFunctionTree(manager, node);
									Refresh(true);
								}
								break;

							case TreeNodeType.ProcedureTitleNode:
								if (node.Children.Count == 0)
								{
									TreeViewManager.MakeProcedureTree(manager, node);
									Refresh(true);
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
		#region FILE
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
		#region VIEW
		private void ViewMenu_Click(object sender, RoutedEventArgs e)
		{
			if (sender is not MenuItem menuItem)
			{
				return;
			}

			var name = menuItem.Header.ToString() ?? string.Empty;

			ShowOrFocusPane(name, () => new LayoutAnchorable
			{
				Title = name
			});
		}
		#endregion
		#region TOOL
		private void ExternalExplorer_Click(object sender, RoutedEventArgs e)
		{
			if (sender is not MenuItem menuItem)
			{
				return;
			}

			var view = new ExternalExplorerView
			{
				DatabaseTreeView = DatabaseTreeView,
				EntryDocumentPane = EntryDocumentPane
			};
			view.Show();
		}
		#endregion
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

		public async void TreeViewMenuItem_Click(object sender, RoutedEventArgs e)
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
							await TreeViewContextMenu.ProcessDatabaseNodeMenu(function, node, manager, EntryDocumentPane);
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

		List<LayoutAnchorable> closedAnchorables = [];
		private void LayoutAnchorable_Hiding(object sender, System.ComponentModel.CancelEventArgs e)
		{
			if (sender is LayoutAnchorable anchorable)
			{
				closedAnchorables.Add(anchorable);
			}
		}

		private void ShowOrFocusPane(string title, Func<LayoutAnchorable> createAnchorable)
		{
			var existingAnchorable = closedAnchorables.FirstOrDefault(a => a.Title == title);
			if (existingAnchorable != null)
			{
				existingAnchorable.Show();
				MainDockingManager.ActiveContent = existingAnchorable;
				closedAnchorables.Remove(existingAnchorable);
			}
			else
			{
				var newAnchorable = createAnchorable();
				var pane = MainDockingManager.Layout.Descendents().OfType<LayoutAnchorablePane>().FirstOrDefault();
				if (pane != null)
				{
					pane.Children.Add(newAnchorable);
					newAnchorable.Show();
					MainDockingManager.ActiveContent = newAnchorable;
				}
			}
		}

		#region LOGS MENU
		private void CopyLog_Click(object sender, RoutedEventArgs e)
		{
			Clipboard.SetText(GetLogText());
		}

		private void CopyLatestLog_Click(object sender, RoutedEventArgs e)
		{
			Clipboard.SetText(GetLogText().Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries).Last());
		}

		private string GetLogText()
		{
			return new TextRange(StatusTextBlock.Document.ContentStart, StatusTextBlock.Document.ContentEnd).Text;
		}
		#endregion
	}
}