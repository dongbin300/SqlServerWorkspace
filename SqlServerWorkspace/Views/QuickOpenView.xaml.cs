using AvalonDock.Layout;

using SqlServerWorkspace.DataModels;
using SqlServerWorkspace.Enums;

using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace SqlServerWorkspace.Views
{
	/// <summary>
	/// QuickOpenView.xaml에 대한 상호 작용 논리
	/// </summary>
	public partial class QuickOpenView : Window
	{
		public TreeNode CurrentDatabaseNode = default!;
		public TreeView DatabaseTreeView = default!;
		public LayoutDocumentPane EntryDocumentPane = default!;
		public TreeNode SelectedNode = default!;

		private List<TreeNode> _allNodes = [];
		//private bool _isEscPressed = false;

		public QuickOpenView()
		{
			InitializeComponent();
			PreviewKeyDown += QuickOpenView_PreviewKeyDown;
		}

		private void Window_Loaded(object sender, RoutedEventArgs e)
		{
			// 초기 로드
			LoadNodesForCurrentDatabase();

			// 포커스를 FilterTextBox로 이동
			FilterTextBox.Focus();
		}

		private void LoadNodesForCurrentDatabase()
		{
			if (CurrentDatabaseNode == null)
				return;

			// Cache에서 현재 데이터베이스의 노드들 찾기
			var cachedNodes = Cache.TreeNodes
				.Where(n => n.GetDatabaseName() == CurrentDatabaseNode.Name)
				.ToList();

			if (cachedNodes.Count != 0)
			{
				_allNodes = cachedNodes;
			}
			else
			{
				LoadNodesFromDatabase();
			}

			// 필터 적용
			ApplyFilter();
		}

		private void LoadNodesFromDatabase()
		{
			if (CurrentDatabaseNode == null)
				return;

			var manager = DatabaseTreeView.GetSqlManager(CurrentDatabaseNode);
			if (manager == null)
				return;

			_allNodes.Clear();

			try
			{
				var basePath = CurrentDatabaseNode.Path;

				// 테이블 로드
				var tableNames = manager.SelectTableNames();
				foreach (var tableName in tableNames)
				{
					var (svgData, svgColor) = GetIconForNodeType(TreeNodeType.TableNode);
					var tableNode = new TreeNode(
						name: tableName,
						type: TreeNodeType.TableNode,
						path: $"{basePath}/Tables/{tableName}",
						svgData: svgData,
						svgColor: svgColor
					);
					_allNodes.Add(tableNode);
					Cache.TreeNodes.Add(tableNode);
				}

				// 뷰 로드
				var viewNames = manager.SelectViewNames();
				foreach (var viewName in viewNames)
				{
					var (svgData, svgColor) = GetIconForNodeType(TreeNodeType.ViewNode);
					var viewNode = new TreeNode(
						name: viewName,
						type: TreeNodeType.ViewNode,
						path: $"{basePath}/Views/{viewName}",
						svgData: svgData,
						svgColor: svgColor
					);
					_allNodes.Add(viewNode);
					Cache.TreeNodes.Add(viewNode);
				}

				// 함수 로드
				var functionNames = manager.SelectFunctionNames();
				foreach (var functionName in functionNames)
				{
					var (svgData, svgColor) = GetIconForNodeType(TreeNodeType.FunctionNode);
					var functionNode = new TreeNode(
						name: functionName,
						type: TreeNodeType.FunctionNode,
						path: $"{basePath}/Functions/{functionName}",
						svgData: svgData,
						svgColor: svgColor
					);
					_allNodes.Add(functionNode);
					Cache.TreeNodes.Add(functionNode);
				}

				// 프로시저 로드
				var procedureNames = manager.SelectProcedureNames();
				foreach (var procedureName in procedureNames)
				{
					var (svgData, svgColor) = GetIconForNodeType(TreeNodeType.ProcedureNode);
					var procedureNode = new TreeNode(
						name: procedureName,
						type: TreeNodeType.ProcedureNode,
						path: $"{basePath}/Procedures/{procedureName}",
						svgData: svgData,
						svgColor: svgColor
					);
					_allNodes.Add(procedureNode);
					Cache.TreeNodes.Add(procedureNode);
				}
			}
			catch (Exception ex)
			{
				MessageBox.Show($"데이터베이스 로드 오류: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
			}
		}

		private (string SvgData, string SvgColor) GetIconForNodeType(TreeNodeType type)
		{
			return type switch
			{
				TreeNodeType.TableNode => (ResourceManager.TableIcon, ResourceManager.TableIconColor),
				TreeNodeType.ViewNode => (ResourceManager.ViewIcon, ResourceManager.ViewIconColor),
				TreeNodeType.FunctionNode => (ResourceManager.FunctionIcon, ResourceManager.FunctionIconColor),
				TreeNodeType.ProcedureNode => (ResourceManager.ProcedureIcon, ResourceManager.ProcedureIconColor),

				// 서버, 데이터베이스 등 다른 타입이 필요하면 추가
				// TreeNodeType.DatabaseNode   => (ResourceManager.DatabaseIcon, ResourceManager.DatabaseIconColor),

				_ => ("", "#FFFFFF")  // 기본값 (투명 또는 흰색)
			};
		}

		private void FilterTextBox_KeyDown(object sender, KeyEventArgs e)
		{
			if (e.Key == Key.Down)
			{
				if (ItemListBox.Items.Count > 0)
				{
					ItemListBox.Focus();
					ItemListBox.SelectedIndex = 0;
				}
				e.Handled = true;
			}
			else if (e.Key == Key.Enter)
			{
				if (ItemListBox.SelectedItem != null)
				{
					OpenSelectedItem();
				}
				e.Handled = true;
			}
		}

		private void ApplyFilter()
		{
			var filterText = FilterTextBox.Text?.Trim().ToLower() ?? string.Empty;

			if (string.IsNullOrWhiteSpace(filterText))
			{
				ItemListBox.ItemsSource = _allNodes;
			}
			else
			{
				var filtered = _allNodes
					.Where(n => n.Name?.ToLower().Contains(filterText) == true)
					.ToList();
				ItemListBox.ItemsSource = filtered;
			}

			// 필터 후 자동으로 첫 번째 항목 선택 (UX 향상)
			if (ItemListBox.Items.Count > 0)
			{
				ItemListBox.SelectedIndex = 0;
				// 포커스도 리스트로 이동시키면 더 직관적 (선택 사항)
				// ItemListBox.Focus();
			}
			else
			{
				ItemListBox.SelectedIndex = -1;
			}
		}

		private void ItemListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			// 선택 변경 처리 (필요시)
		}

		private void ItemListBox_MouseDoubleClick(object sender, MouseButtonEventArgs e)
		{
			OpenSelectedItem();
		}

		private void OpenSelectedItem()
		{
			if (ItemListBox.SelectedItem is TreeNode selectedNode)
			{
				SelectedNode = selectedNode;
				DialogResult = true;
				Close();
			}
		}

		private void QuickOpenView_PreviewKeyDown(object sender, KeyEventArgs e)
		{
			if (e.Key == Key.Escape)
			{
				if (!string.IsNullOrEmpty(FilterTextBox.Text))
				{
					// 첫 번째 ESC: 필터 초기화
					FilterTextBox.Clear();
					FilterTextBox.Focus();
					//_isEscPressed = true;
					e.Handled = true;
					return;
				}
				else
				{
					// 두 번째 ESC: 창 닫기
					DialogResult = false;
					Close();
					e.Handled = true;
					return;
				}
			}

			// ESC가 아닌 다른 키 → 플래그 리셋
			//_isEscPressed = false;

			// Enter 키 통합 처리 (어디서 누르든 선택된 항목 열기)
			if (e.Key == Key.Enter)
			{
				if (ItemListBox.SelectedItem != null)
				{
					OpenSelectedItem();
					e.Handled = true;
				}
				// 선택된 항목이 없으면 아무것도 안 함 (필요시 메시지 추가 가능)
				return;
			}

			// 방향키 처리 (필요한 경우에만)
			if (e.Key == Key.Down)
			{
				if (FilterTextBox.IsFocused && ItemListBox.Items.Count > 0)
				{
					ItemListBox.Focus();
					ItemListBox.SelectedIndex = 0;
					e.Handled = true;
				}
				else if (ItemListBox.IsKeyboardFocusWithin &&
						 ItemListBox.SelectedIndex == ItemListBox.Items.Count - 1)
				{
					// 마지막 항목에서 아래 → 텍스트박스로 이동
					FilterTextBox.Focus();
					FilterTextBox.SelectAll();
					e.Handled = true;
				}
			}
			else if (e.Key == Key.Up)
			{
				if (FilterTextBox.IsFocused && ItemListBox.Items.Count > 0)
				{
					ItemListBox.Focus();
					ItemListBox.SelectedIndex = ItemListBox.Items.Count - 1;
					e.Handled = true;
				}
			}
		}

		private void FilterTextBox_TextChanged(object sender, TextChangedEventArgs e)
		{
			ApplyFilter();
		}

		private void ItemListBox_PreviewKeyDown(object sender, KeyEventArgs e)
		{
			if (e.Key == Key.Enter && ItemListBox.SelectedItem != null)
			{
				OpenSelectedItem();
				e.Handled = true;
			}
		}
	}
}