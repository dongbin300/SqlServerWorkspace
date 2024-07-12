using Microsoft.Web.WebView2.Core;

using SqlServerWorkspace.Data;

using System.Data;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;

namespace SqlServerWorkspace.Views.Controls
{
	/// <summary>
	/// TableViewControl.xaml에 대한 상호 작용 논리
	/// </summary>
	public partial class TableViewControl : UserControl
	{
		public SqlManager Manager { get; set; } = default!;
		public string Header { get; set; } = string.Empty;

		public TableViewControl()
		{
			InitializeComponent();
		}

		private async void UserControl_Loaded(object sender, RoutedEventArgs e)
		{
			await WebView.Init();
		}

		private void ExecuteQuery(string query)
		{
			try
			{
				var table = Manager.Select(query);
				TableDataGrid.Columns.Clear();
				TableDataGrid.ItemsSource = table.DefaultView;

				foreach (DataColumn column in table.Columns)
				{
					var binding = new Binding(column.ColumnName)
					{
						//Converter = (IValueConverter)Application.Current.FindResource("DBNullToNullStringConverter")
					};

					if (column.DataType == typeof(DateTime))
					{
						binding.StringFormat = "yyyy-MM-dd HH:mm:ss";
					}

					var dataGridColumn = new DataGridTextColumn
					{
						Header = column.ColumnName,
						Binding = binding
					};

					var headerTemplate = new DataTemplate();
					var textBlockFactory = new FrameworkElementFactory(typeof(TextBlock));
					textBlockFactory.SetBinding(TextBlock.TextProperty, new Binding());
					headerTemplate.VisualTree = textBlockFactory;
					dataGridColumn.HeaderTemplate = headerTemplate;

					TableDataGrid.Columns.Add(dataGridColumn);
				}

				//var rowNumberColumn = new DataGridTextColumn
				//{
				//	Header = "#",
				//	Binding = new Binding
				//	{
				//		Path = new PropertyPath("Items.IndexOf"),
				//		RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor) { AncestorType = typeof(DataGridRow) }
				//	}
				//};
				//dataGrid.Columns.Insert(0, rowNumberColumn);

				Common.SetStatusText("Complete");
			}
			catch (Exception ex)
			{
				Common.SetStatusText(ex.Message);
			}
		}

		private async void RunButton_Click(object sender, RoutedEventArgs e)
		{
			var query = await WebView.GetEditorText();
			ExecuteQuery(query);
		}

		private void SaveButton_Click(object sender, RoutedEventArgs e)
		{

        }

		private async void WebView_NavigationCompleted(object sender, CoreWebView2NavigationCompletedEventArgs e)
		{
			if (e.IsSuccess)
			{
				await WebView.SetEditorText($"SELECT * FROM {Header}");
				var query = await WebView.GetEditorText();
				ExecuteQuery(query);
			}
		}

		private void WebView_KeyDown(object sender, KeyEventArgs e)
		{
			switch (e.Key)
			{
				case Key.F5:
					RunButton_Click(sender, e);
					break;
			}
		}
	}
}
