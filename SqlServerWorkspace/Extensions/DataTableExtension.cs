using System.Data;

namespace SqlServerWorkspace.Extensions
{
	public static class DataTableExtension
    {
		public static IEnumerable<string> Field(this DataTable dataTable, string fieldName)
		{
			return dataTable.Rows.Cast<DataRow>().Where(row => row[fieldName] != DBNull.Value).Select(row => row[fieldName].ToString() ?? string.Empty);
		}
	}
}
