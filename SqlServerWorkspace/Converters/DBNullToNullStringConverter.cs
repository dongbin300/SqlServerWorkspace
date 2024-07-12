using System.Windows.Data;

namespace SqlServerWorkspace.Converters
{
	public class DBNullToNullStringConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
		{
			//return value == DBNull.Value ? "(null)" : value;
			return value == DBNull.Value ? "" : value;
		}

		public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
		{
			throw new NotImplementedException();
		}
	}
}
