using SqlServerWorkspace.DataModels;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace SqlServerWorkspace.Views.Panes
{
	/// <summary>
	/// ObjectExplorer.xaml에 대한 상호 작용 논리
	/// </summary>
	public partial class ObjectExplorer : UserControl
	{
		public ObjectExplorer()
		{
			InitializeComponent();
		}

		public void Refresh()
		{
			var keyword = DatabaseTreeViewFilterTextBox.Text;

			DatabaseTreeView.ItemsSource = DatabaseTreeViewFilterTextBox.Text.Length < 1 ?
				ResourceManager.ConnectionsNodes :
				Filter(ResourceManager.ConnectionsNodes, keyword);
		}

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

		private void DatabaseTreeView_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
		{

		}
	}
}
