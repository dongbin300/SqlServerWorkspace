using SqlServerWorkspace.Enums;

using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;

namespace SqlServerWorkspace
{
	public class Common
    {
        public static Window MainWindow => Application.Current.MainWindow;

        public static void ClearLog()
        {
			if (MainWindow is MainWindow mainWindow)
            {
                mainWindow.StatusTextBlock.Inlines.Clear();
			}
		}

		public static void LogSimple(string text)
        {
            if (MainWindow is MainWindow mainWindow)
            {
                var run = new Run(text);
                mainWindow.StatusTextBlock.Inlines.Add(run);
                mainWindow.StatusTextBlock.Inlines.Add(new LineBreak());
                mainWindow.StatusTextScrollViewer.ScrollToEnd();
			}
		}

        public static void Log(string text, LogType type = LogType.Info)
        {
            if (MainWindow is MainWindow mainWindow)
            {
                var currentTab = mainWindow.EntryPane.GetCurrentTab();
                var tabHeader = currentTab == null ? string.Empty : currentTab.Title;

				var run = new Run($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}/{tabHeader}] {text}")
				{
					Foreground = type switch
					{
						LogType.Info => (SolidColorBrush)Application.Current.FindResource("InfoColor"),
						LogType.Success => (SolidColorBrush)Application.Current.FindResource("SuccessColor"),
						LogType.Warning => (SolidColorBrush)Application.Current.FindResource("WarningColor"),
						LogType.Error => (SolidColorBrush)Application.Current.FindResource("ErrorColor"),
						_ => (SolidColorBrush)Application.Current.FindResource("InfoColor")
					}
				};
				mainWindow.StatusTextBlock.Inlines.Add(run);
				mainWindow.StatusTextBlock.Inlines.Add(new LineBreak());
                mainWindow.StatusTextScrollViewer.ScrollToEnd();
			}
        }

        public static void RefreshMainWindow()
        {
			if (MainWindow is MainWindow mainWindow)
            {
                mainWindow.Refresh();
            }
		}
	}
}
