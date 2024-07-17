using System.Windows;

namespace SqlServerWorkspace
{
    public class Common
    {
        public static Window MainWindow => Application.Current.MainWindow;

		public static void SetLog(string text)
        {
            if (MainWindow is MainWindow mainWindow)
            {
                mainWindow.StatusText = text + Environment.NewLine;
            }
        }

        public static void SetLogDetail(string text)
        {
            if (MainWindow is MainWindow mainWindow)
            {
                var currentTab = mainWindow.EntryPane.GetCurrentTab();
                var tabHeader = currentTab == null ? string.Empty : currentTab.Title;
                mainWindow.StatusText = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}/{tabHeader}] {text}" + Environment.NewLine;
            }
        }

        public static void AppendLog(string text)
        {
            if (MainWindow is MainWindow mainWindow)
            {
                mainWindow.StatusText += text + Environment.NewLine;
            }
        }

        public static void AppendLogDetail(string text)
        {
            if (MainWindow is MainWindow mainWindow)
            {
                var currentTab = mainWindow.EntryPane.GetCurrentTab();
                var tabHeader = currentTab == null ? string.Empty : currentTab.Title;
                mainWindow.StatusText += $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}/{tabHeader}] {text}" + Environment.NewLine;
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
