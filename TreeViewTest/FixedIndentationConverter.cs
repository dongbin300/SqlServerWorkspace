using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace TreeViewTest
{
	public class FixedIndentationConverter : IValueConverter
	{
		public double Indentation { get; set; }

		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			if (value == null || !(value is int))
				return new Thickness(0);

			return new Thickness((int)value * Indentation, 0, 0, 0);
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			throw new NotImplementedException();
		}
	}
}
