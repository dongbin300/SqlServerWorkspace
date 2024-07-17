using System.Windows;

namespace SqlServerWorkspace.Views
{
	/// <summary>
	/// RenameView.xaml에 대한 상호 작용 논리
	/// </summary>
	public partial class RenameView : Window
	{
		public string RenameText = string.Empty;

		public RenameView()
		{
			InitializeComponent();
		}

		private void Window_Loaded(object sender, RoutedEventArgs e)
		{
			RenameTextBox.Text = RenameText;
			RenameTextBox.Focus();
			RenameTextBox.SelectAll();
		}

		private void OkButton_Click(object sender, RoutedEventArgs e)
		{
			RenameText = RenameTextBox.Text;

			DialogResult = true;
			Close();
		}

		private void CancelButton_Click(object sender, RoutedEventArgs e)
		{
			DialogResult = false;
			Close();
		}

		private void RenameTextBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
		{
			if (e.Key == System.Windows.Input.Key.Enter)
			{
				OkButton_Click(sender, e);
			}
		}
	}
}
