using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;

using System.IO;
using System.Windows;
using System.Windows.Media;

namespace TreeViewTest
{
	/// <summary>
	/// Interaction logic for MainWindow.xaml
	/// </summary>
	public partial class MainWindow : Window
	{
		static readonly string _monacoHtmlPath = Path.Combine("Resources", "monaco.html");
		static readonly string _userDataFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SqlServerWorkspace");

		WebView2 webView;

		public MainWindow()
		{
			InitializeComponent();
		}

		public static async Task InitWebView(WebView2 webView)
		{
			try
			{
				var env = await CoreWebView2Environment.CreateAsync(null, _userDataFolder);
				await webView.EnsureCoreWebView2Async(env);
				webView.CoreWebView2.Settings.IsScriptEnabled = true;
				webView.CoreWebView2.NavigateToString(File.ReadAllText(_monacoHtmlPath));
			}
			catch (ArgumentException)
			{

			}
		}

		private async void TestButton_Click(object sender, RoutedEventArgs e)
		{
			TestButton.Visibility = Visibility.Collapsed;

			webView = new WebView2
			{
				HorizontalAlignment = HorizontalAlignment.Left,
				VerticalAlignment = VerticalAlignment.Top
			};

			MainGrid.Children.Add(webView);

			await InitWebView(webView);

			SetWebViewSize(Width, Height);
		}

		private void SetWebViewSize(double width, double height)
		{
			webView.Width = width;
			webView.Height = height;

			var scaleX = width / webView.ActualWidth;
			var scaleY = height / webView.ActualHeight;
			webView.RenderTransform = new ScaleTransform(scaleX, scaleY);
		}

		private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
		{
			if(webView != null)
			{
				SetWebViewSize(Width, Height);
			}

			//var webViews = MainGrid.Children.OfType<WebView2>();
			//if (webViews.Any())
			//{
			//	var webView = webViews.First();
			//	webView.Width = Width;
			//	webView.Height = Height;
			//	webView.UpdateLayout();
			//}
		}
	}
}