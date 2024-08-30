using System.Windows;
using System.Windows.Media;

namespace SqlServerWorkspace.Extensions
{
	public static class DependencyObjectExtension
	{
		public static T? FindParent<T>(this DependencyObject child) where T : DependencyObject
		{
			DependencyObject parentObject = VisualTreeHelper.GetParent(child);
			return parentObject == null ? null : parentObject is T parent ? parent : FindParent<T>(parentObject);
		}
	}
}
