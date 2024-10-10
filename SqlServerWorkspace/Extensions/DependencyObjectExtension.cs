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

		public static T? FindVisualChild<T>(this DependencyObject parent) where T : DependencyObject
		{
			for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
			{
				var child = VisualTreeHelper.GetChild(parent, i);
				if (child is T typedChild)
				{
					return typedChild;
				}

				var childOfChild = FindVisualChild<T>(child);
				if (childOfChild != null)
				{
					return childOfChild;
				}
			}
			return null;
		}
	}
}
