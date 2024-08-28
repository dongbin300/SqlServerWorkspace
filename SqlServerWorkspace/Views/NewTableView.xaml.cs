using SqlServerWorkspace.Data;

using System.Windows;

namespace SqlServerWorkspace.Views
{
	public class NewTableView_ColumnDataGrid
	{
		public string Name { get; set; } = string.Empty;
		public string DataType { get; set; } = string.Empty;
		public string Description { get; set; } = string.Empty;
		public bool Key { get; set; }
		public bool NotNull { get; set; }
	}

    /// <summary>
    /// NewTableView.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class NewTableView : Window
    {
		public SqlManager Manager = default!;

        public NewTableView()
        {
            InitializeComponent();

			ColumnDataGrid.ItemsSource = new List<NewTableView_ColumnDataGrid>();
		}

		private void OkButton_Click(object sender, RoutedEventArgs e)
		{
			var columns = new List<TableColumnInfo>();
			for (int i = 0; i < ColumnDataGrid.Items.Count; i++)
			{
				if (ColumnDataGrid.Items[i] is not NewTableView_ColumnDataGrid item)
				{
					continue;
				}

				if (item.Name == string.Empty)
				{
					continue;
				}

				columns.Add(new TableColumnInfo(item.Name, item.DataType, item.Key, item.NotNull, item.Description));
			}

			var result = Manager.NewTable(TableNameTextBox.Text, columns);
			if (result != string.Empty)
			{
				MessageBox.Show(result);
				return;
			}

			DialogResult = true;
			Close();
		}

		private void CancelButton_Click(object sender, RoutedEventArgs e)
		{
			DialogResult = false;
			Close();
		}
	}
}
