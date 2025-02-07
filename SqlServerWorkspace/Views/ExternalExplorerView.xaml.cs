using AvalonDock.Layout;

using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace SqlServerWorkspace.Views
{
	/// <summary>
	/// ExternalExplorerView.xaml에 대한 상호 작용 논리
	/// </summary>
	public partial class ExternalExplorerView : Window
	{
		public TreeView DatabaseTreeView = default!;
		public LayoutDocumentPane EntryDocumentPane = default!;

		List<string> fileList = [];
		List<string> procedureList = [];
		List<string> tableList = [];

		public ExternalExplorerView()
		{
			InitializeComponent();
		}

		private void Window_Loaded(object sender, RoutedEventArgs e)
		{
			SearchDirectoryTextBox.Text = ResourceManager.Settings.ExternalExplorerSearchDirectory;
			SearchFilePatternTextBox.Text = ResourceManager.Settings.ExternalExplorerSearchFilePattern;
			SearchProcedurePatternTextBox.Text = ResourceManager.Settings.ExternalExplorerSearchProcedurePattern;

			var topNodeItems = DatabaseTreeView.GetAllTopLevelNodes();
			var topNodes = topNodeItems.Select(x => x.GetNode() ?? default!);
			var topNodeNames = topNodes.Select(x => x.Name);
			ServerComboBox.ItemsSource = topNodeNames;

			if (ServerComboBox.Items.Count > 0)
			{
				ServerComboBox.SelectedIndex = 0;
			}
		}

		private void SearchButton_Click(object sender, RoutedEventArgs e)
		{
			var searchDirectory = SearchDirectoryTextBox.Text;
			var searchFilePattern = SearchFilePatternTextBox.Text;
			var searchProcedurePattern = SearchProcedurePatternTextBox.Text;

			ResourceManager.Settings.ExternalExplorerSearchDirectory = searchDirectory;
			ResourceManager.Settings.ExternalExplorerSearchFilePattern = searchFilePattern;
			ResourceManager.Settings.ExternalExplorerSearchProcedurePattern = searchProcedurePattern;
			ResourceManager.SaveSettings();

			FileListBox.ItemsSource = null;
			ProcedureListBox.ItemsSource = null;
			TableListBox.ItemsSource = null;

			var fileNames = Directory.GetFiles(searchDirectory, searchFilePattern, SearchOption.AllDirectories).Select(x => Path.GetRelativePath(searchDirectory, x)).ToList();
			fileList = fileNames;
			FileListBox.ItemsSource = fileList;
		}

		private void FileListFilterTextBox_KeyDown(object sender, KeyEventArgs e)
		{
			if (e.Key == Key.Enter)
			{
				var keyword = FileListFilterTextBox.Text;

				FileListBox.ItemsSource = keyword.Length < 1 ? fileList : fileList.Where(x => x.Contains(keyword, StringComparison.OrdinalIgnoreCase)).ToList();
			}
		}

		private void ProcedureListFilterTextBox_KeyDown(object sender, KeyEventArgs e)
		{
			if (e.Key == Key.Enter)
			{
				var keyword = ProcedureListFilterTextBox.Text;

				ProcedureListBox.ItemsSource = keyword.Length < 1 ? procedureList : procedureList.Where(x => x.Contains(keyword, StringComparison.OrdinalIgnoreCase)).ToList();
			}
		}

		private void TableListFilterTextBox_KeyDown(object sender, KeyEventArgs e)
		{
			if (e.Key == Key.Enter)
			{
				var keyword = TableListFilterTextBox.Text;

				TableListBox.ItemsSource = keyword.Length < 1 ? tableList : tableList.Where(x => x.Contains(keyword, StringComparison.OrdinalIgnoreCase)).ToList();
			}
		}

		public static HashSet<string> SearchKeywords(string text, string keyword)
		{
			HashSet<string> results = [];

			string regexPattern = Regex.Escape(keyword).Replace("\\*", ".*");
			string fullPattern = $"\"({regexPattern})\"";

			var regex = new Regex(fullPattern);
			foreach (Match match in regex.Matches(text))
			{
				if (match.Groups.Count > 1)
				{
					results.Add(match.Groups[1].Value);
				}
			}

			return results;
		}

		public static HashSet<string> GetReferencedTables(string spText)
		{
			var tables = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			var pattern = @"\b(?:FROM|JOIN|INTO|UPDATE|DELETE FROM)\s+([^,\s;(]+)";
			var matches = Regex.Matches(spText, pattern, RegexOptions.IgnoreCase);

			foreach (Match match in matches)
			{
				if (!match.Groups[1].Success) continue;

				string tableName = match.Groups[1].Value.Trim();

				tableName = Regex.Replace(tableName, @"[\[\]]", "");

				if (tableName.Contains("(")) continue;

				tables.Add(tableName);
			}

			return tables;
		}

		private void FileListBox_MouseDoubleClick(object sender, MouseButtonEventArgs e)
		{

		}

		private void FileListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			if (e.AddedItems.Count == 0 || e.AddedItems[0] == null)
			{
				return;
			}

			var selectedItem = e.AddedItems[0];
			if (selectedItem == null)
			{
				return;
			}

			var selectedFileName = selectedItem.ToString();
			if (string.IsNullOrEmpty(selectedFileName))
			{
				return;
			}

			var filePath = Path.Combine(SearchDirectoryTextBox.Text, selectedFileName);
			var pattern = SearchProcedurePatternTextBox.Text;
			var text = File.ReadAllText(filePath);

			var matches = SearchKeywords(text, pattern);

			procedureList = [.. matches];
			ProcedureListBox.ItemsSource = procedureList;
		}

		private void ProcedureListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			if (e.AddedItems.Count == 0 || e.AddedItems[0] == null)
			{
				return;
			}

			var selectedItem = e.AddedItems[0];
			if (selectedItem == null)
			{
				return;
			}

			var selectedProcedureName = selectedItem.ToString();
			if (string.IsNullOrEmpty(selectedProcedureName))
			{
				return;
			}

			var serverId = ServerComboBox.SelectedItem.ToString();
			if (string.IsNullOrEmpty(serverId))
			{
				return;
			}

			var manager = ResourceManager.GetSqlManager(serverId);
			if (manager == null)
			{
				return;
			}

			var procedureText = manager.GetObject(selectedProcedureName);
			var tables = GetReferencedTables(procedureText);

			TableListBox.ItemsSource = tables;
		}

		private async void ProcedureListBox_MouseDoubleClick(object sender, MouseButtonEventArgs e)
		{
			if (sender is not ListBox listBox)
			{
				return;
			}

			if (listBox.SelectedItems == null)
			{
				return;
			}

			var selectedProcedure = listBox.SelectedItem.ToString();
			if (string.IsNullOrEmpty(selectedProcedure))
			{
				return;
			}

			var serverId = ServerComboBox.SelectedItem.ToString();
			if (string.IsNullOrEmpty(serverId))
			{
				return;
			}

			var manager = ResourceManager.GetSqlManager(serverId);
			if (manager == null)
			{
				return;
			}

			await EntryDocumentPane.CreateNewOrOpenTab(manager, selectedProcedure, Enums.TreeNodeType.ProcedureNode);
		}

		private void TableListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{

		}

		private async void TableListBox_MouseDoubleClick(object sender, MouseButtonEventArgs e)
		{
			if (sender is not ListBox listBox)
			{
				return;
			}

			if (listBox.SelectedItems == null)
			{
				return;
			}

			var selectedTable = listBox.SelectedItem.ToString();
			if (string.IsNullOrEmpty(selectedTable))
			{
				return;
			}

			var serverId = ServerComboBox.SelectedItem.ToString();
			if (string.IsNullOrEmpty(serverId))
			{
				return;
			}

			var manager = ResourceManager.GetSqlManager(serverId);
			if (manager == null)
			{
				return;
			}

			await EntryDocumentPane.CreateNewOrOpenTab(manager, selectedTable, Enums.TreeNodeType.TableNode);
		}
	}
}
