using System.Data;
using System.Windows.Controls;

namespace SqlServerWorkspace.Extensions
{
	public static class DataTableExtension
	{
		public static IEnumerable<string> Field(this DataTable dataTable, string fieldName)
		{
			return dataTable.Rows.Cast<DataRow>().Where(row => row[fieldName] != DBNull.Value).Select(row => row[fieldName].ToString() ?? string.Empty);
		}

		public static DataTable ToDataTable(this DataGrid dataGrid)
		{
			var dataTable = new DataTable();

			foreach (var column in dataGrid.Columns)
			{
				if (column is DataGridTextColumn textColumn)
				{
					dataTable.Columns.Add(textColumn.Header.ToString(), typeof(string));
				}
			}

			for (var i = 0; i < dataGrid.Items.Count; i++)
			{
				if (dataGrid.Items[i] is not DataRowView item)
				{
					continue;
				}

				DataRow row = dataTable.NewRow();
				foreach (var column in dataGrid.Columns)
				{
					if (column is DataGridTextColumn textColumn)
					{
						var columnName = textColumn.Header.ToString();
						if (string.IsNullOrEmpty(columnName))
						{
							continue;
						}

						var value = item[columnName];
						row[columnName] = value ?? DBNull.Value;
					}
				}
				dataTable.Rows.Add(row);
			}

			return dataTable;
		}
	}
}
