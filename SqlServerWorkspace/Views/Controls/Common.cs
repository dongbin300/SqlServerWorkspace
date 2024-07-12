using System.Windows;

namespace SqlServerWorkspace.Views.Controls
{
	public class Common
	{
		public static void SetStatusText(string text)
		{
			if (Application.Current.MainWindow is MainWindow mainWindow)
			{
				mainWindow.StatusText = text;
			}
		}
	}
}
