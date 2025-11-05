using System.Windows;

namespace SqlServerWorkspace.Views
{
	/// <summary>
	/// NameView.xaml에 대한 상호 작용 논리
	/// </summary>
	public partial class NameView : Window
	{
		public string NameText = string.Empty;

		public NameView()
		{
			InitializeComponent();
		}

		private void Window_Loaded(object sender, RoutedEventArgs e)
		{
			NameTextBox.Text = NameText;
			NameTextBox.Focus();
			NameTextBox.SelectAll();
		}

		private void OkButton_Click(object sender, RoutedEventArgs e)
		{
			NameText = NameTextBox.Text?.Trim() ?? string.Empty;

			// 유효성 검사: 빈 이름 허용 안함
			if (string.IsNullOrWhiteSpace(NameText))
			{
				MessageBox.Show("이름을 입력해주세요.", "유효성 오류", MessageBoxButton.OK, MessageBoxImage.Warning);
				NameTextBox.Focus();
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

		private void NameTextBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
		{
			if (e.Key == System.Windows.Input.Key.Enter)
			{
				OkButton_Click(sender, e);
			}
		}
	}
}
