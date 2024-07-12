using SqlServerWorkspace.Data;
using SqlServerWorkspace.Enums;
using SqlServerWorkspace.Extensions;

using System.Windows;
using System.Windows.Controls;

namespace SqlServerWorkspace.Views
{
	/// <summary>
	/// ConnectionView.xaml에 대한 상호 작용 논리
	/// </summary>
	public partial class ConnectionView : Window
	{
		public ConnectionView()
		{
			InitializeComponent();
		}

		private void OkButton_Click(object sender, RoutedEventArgs e)
		{
			var authenticationType = (AuthenticationComboBox.SelectionBoxItem.ToString() ?? string.Empty).ToAuthenticationType();
			var server = ServerTextBox.Text;
			var database = DatabaseTextBox.Text;
			var user = UserTextBox.Text;
			var password = PasswordTextBox.Password;
			SqlManager connectionInfo = default!;
			switch (authenticationType)
			{
				case AuthenticationType.SqlServerAuthentication:
					connectionInfo = new SqlManager(authenticationType, server)
					{
						Database = database,
						User = user,
						Password = password
					};
					break;

				case AuthenticationType.WindowsAuthentication:
					connectionInfo = new SqlManager(authenticationType, server)
					{
						Database = database
					};
					break;

				default:
					break;
			}
			ResourceManager.Connections.Add(connectionInfo);
			ResourceManager.SaveConnectionInfo();

			DialogResult = true;
			Close();
		}

		private void CancelButton_Click(object sender, RoutedEventArgs e)
		{
			DialogResult = false;
			Close();
		}

		private void AuthenticationComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			UserTextBox.IsEnabled = AuthenticationComboBox.SelectedIndex == 0;
			PasswordTextBox.IsEnabled = AuthenticationComboBox.SelectedIndex == 0;
		}
	}
}
