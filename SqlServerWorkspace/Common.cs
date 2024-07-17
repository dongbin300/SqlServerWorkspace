using System.Windows;

namespace SqlServerWorkspace
{
    public class Common
    {
        public static void SetLog(string text)
        {
            if (Application.Current.MainWindow is MainWindow mainWindow)
            {
                mainWindow.StatusText = text + Environment.NewLine;
            }
        }

        public static void SetLogDetail(string text)
        {
            if (Application.Current.MainWindow is MainWindow mainWindow)
            {
                var currentTab = mainWindow.EntryPane.GetCurrentTab();
                var tabHeader = currentTab == null ? string.Empty : currentTab.Title;
                mainWindow.StatusText = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}/{tabHeader}] {text}" + Environment.NewLine;
            }
        }

        public static void AppendLog(string text)
        {
            if (Application.Current.MainWindow is MainWindow mainWindow)
            {
                mainWindow.StatusText += text + Environment.NewLine;
            }
        }

        public static void AppendLogDetail(string text)
        {
            if (Application.Current.MainWindow is MainWindow mainWindow)
            {
                var currentTab = mainWindow.EntryPane.GetCurrentTab();
                var tabHeader = currentTab == null ? string.Empty : currentTab.Title;
                mainWindow.StatusText += $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}/{tabHeader}] {text}" + Environment.NewLine;
            }
        }

        public static void RefreshMainWindow()
        {
			if (Application.Current.MainWindow is MainWindow mainWindow)
            {
                mainWindow.Refresh();
            }
		}
	}
}
