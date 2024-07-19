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
				if (column is DataGridColumn dataGridColumn)
				{
					var columnName = ((string[])dataGridColumn.Header)[0];
					dataTable.Columns.Add(columnName, typeof(string));
				}
			}

			foreach(var item in dataGrid.Items)
			{
				if (item is not DataRowView row)
				{
					continue;
				}

				DataRow newRow = dataTable.NewRow();
				foreach (var column in dataGrid.Columns)
				{
					if (column is DataGridColumn dataGridColumn)
					{
						var columnName = ((string[])dataGridColumn.Header)[0];
						if (string.IsNullOrEmpty(columnName))
						{
							continue;
						}

						var value = row[columnName];

						if (value is DateTime dateTime)
						{
							value = dateTime.ToString("yyyy-MM-dd HH:mm:ss");
						}

						newRow[columnName] = value ?? DBNull.Value;
					}
				}
				dataTable.Rows.Add(newRow);
			}
			return dataTable;
		}
	}
}
