namespace SqlServerWorkspace.Data
{
	public class TableInfo(string name, string catalog, string schema, List<TableColumnInfo> columns)
	{
		public string Name { get; set; } = name;
		public string Catalog { get; set; } = catalog;
		public string Schema { get; set; } = schema;
		public List<TableColumnInfo> Columns { get; set; } = columns;

		public TableColumnInfo GetColumn(string columnName) => Columns.FirstOrDefault(c => c.Name == columnName) ?? default!;
	}
}
