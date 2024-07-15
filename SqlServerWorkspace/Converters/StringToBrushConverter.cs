using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace SqlServerWorkspace.Converters
{
	public class StringToBrushConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			if (value is string colorString)
			{
				if (colorString == string.Empty)
				{
					return Brushes.Transparent;
				}

				var brushConverter = new BrushConverter();
				return brushConverter.ConvertFromString(colorString) ?? Brushes.Transparent;
			}

			return Brushes.Transparent;
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			throw new NotImplementedException();
		}
	}
}
