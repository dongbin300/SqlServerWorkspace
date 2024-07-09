using System.Windows.Media.Imaging;

namespace SqlServerWorkspace
{
	public static class ResourceManager
	{
		public static BitmapImage FunctionIcon = default!;

		public static void Init()
		{
			FunctionIcon = new BitmapImage();
			FunctionIcon.BeginInit();
			FunctionIcon.UriSource = new Uri("pack://application:,,,/Resources/Icons/function.png");
			FunctionIcon.CacheOption = BitmapCacheOption.OnLoad;
			FunctionIcon.EndInit();
		}
	}
}
