namespace SqlServerWorkspace.Data
{
	public class ColumnDescription(string tableName, string columnName, string description)
	{
        public string TableName { get; set; } = tableName;
        public string ColumnName { get; set; } = columnName;
        public string Description { get; set; } = description;
    }
}
