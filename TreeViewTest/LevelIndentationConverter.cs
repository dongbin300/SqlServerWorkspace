using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace TreeViewTest
{
	public class LevelIndentationConverter : IValueConverter
	{
		public double Indentation { get; set; }

		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			int level = (int)value;
			return new Thickness(level * Indentation, 0, 0, 0);
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			throw new NotImplementedException();
		}
	}
}
