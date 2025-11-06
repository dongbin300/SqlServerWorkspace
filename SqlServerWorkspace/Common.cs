using AvalonDock.Layout;

using SqlServerWorkspace.Enums;

using System.Windows;
using System.Windows.Controls;
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
                mainWindow.StatusTextBlock.Document.Blocks.Clear();
                var paragraph = new Paragraph() { Margin = new Thickness(0), LineHeight = double.NaN };
                mainWindow.StatusTextBlock.Document.Blocks.Add(paragraph);
			}
		}

		public static void LogSimple(string text)
        {
            if (MainWindow is MainWindow mainWindow)
            {
                var paragraph = new Paragraph() 
                { 
                    Margin = new Thickness(0), 
                    LineHeight = double.NaN 
                };
                paragraph.Inlines.Add(new Run(text));
                mainWindow.StatusTextBlock.Document.Blocks.Add(paragraph);
                mainWindow.StatusTextBlock.ScrollToEnd();

				SetStatusPanelSelectedIndex("LOG");
			}
		}

        public static void Log(string text, LogType type = LogType.Info)
        {
            if (MainWindow is MainWindow mainWindow)
            {
                var currentTab = mainWindow.EntryDocumentPane.GetCurrentTab();
                var tabHeader = currentTab == null ? string.Empty : currentTab.Title;

                var paragraph = new Paragraph() 
                { 
                    Margin = new Thickness(0), 
                    LineHeight = double.NaN 
                };
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
                
                paragraph.Inlines.Add(run);
                mainWindow.StatusTextBlock.Document.Blocks.Add(paragraph);
                mainWindow.StatusTextBlock.ScrollToEnd();

				SetStatusPanelSelectedIndex("LOG");
			}
        }

        public static void RefreshMainWindow()
        {
			if (MainWindow is MainWindow mainWindow)
            {
                mainWindow.Refresh();
            }
		}

		public static void SetStatusPanelSelectedIndex(string contentId)
		{
            if (MainWindow is MainWindow mainWindow)
			{
				var anchorables = mainWindow.StatusPanel.Children.OfType<LayoutAnchorable>().ToList();
				var targetIndex = anchorables.FindIndex(x => x.ContentId == contentId);

				if (targetIndex >= 0 && targetIndex < anchorables.Count)
				{
					var targetAnchorable = anchorables[targetIndex];

					// 탭을 활성화 상태로 변경
					targetAnchorable.IsActive = true;
					targetAnchorable.IsSelected = true;

					// MainDockingManager로 ActiveContent 설정
					if (mainWindow.MainDockingManager != null)
					{
						mainWindow.MainDockingManager.ActiveContent = targetAnchorable;
					}

					// Fallback: SelectedContentIndex 사용
					mainWindow.StatusPanel.SelectedContentIndex = targetIndex;
				}
			}
		}
	}
}
