using Microsoft.Data.SqlClient;

using SqlServerWorkspace.DataModels;
using SqlServerWorkspace.Enums;
using SqlServerWorkspace.Extensions;

using System.Data;
using System.Text;
using System.Text.Json.Serialization;

namespace SqlServerWorkspace.Data
{
	public class SqlManager(AuthenticationType authenticationType, string server)
	{
		public AuthenticationType AuthenticationType { get; set; } = authenticationType;
		public string Server { get; set; } = server;
		public string User { get; set; } = string.Empty;
		public string Password { get; set; } = string.Empty;
		public string Database { get; set; } = string.Empty;

		[JsonIgnore]
		public List<TreeNode> Nodes { get; set; } = [];

		#region Base SQL Query
		public string GetConnectionString()
		{
			var _connectionString = string.Empty;
			switch (AuthenticationType)
			{
				case AuthenticationType.WindowsAuthentication:
					_connectionString = $"Data Source={Server};Initial Catalog={Database};Integrated Security=True;Encrypt=False";
					break;

				case AuthenticationType.SqlServerAuthentication:
					_connectionString = $"Server={Server};User Id={User};Password={Password};TrustServerCertificate=True;";
					if (Database != string.Empty)
					{
						_connectionString += $"Database={Database};";
					}
					break;

				default:
					break;
			}
			return _connectionString;
		}

		public string Execute(string query)
		{
			using var connection = new SqlConnection(GetConnectionString());
			var command = new SqlCommand(query, connection);

			try
			{
				connection.Open();
				command.ExecuteNonQuery();

				return string.Empty;
			}
			catch (Exception ex)
			{
				return ex.Message;
			}
		}

		public DataTable Select(string query)
		{
			using var connection = new SqlConnection(GetConnectionString());
			var command = new SqlCommand(query, connection);

			try
			{
				connection.Open();
				var reader = command.ExecuteReader();

				var dataTable = new DataTable();
				dataTable.Load(reader);
				reader.Close();

				return dataTable;
			}
			catch
			{
				throw;
			}
		}

		public DataTable Select(string fieldName, string tableName, string condition = "", string order = "")
		{
			var query = $"SELECT {fieldName} FROM {tableName}";
			if (condition != string.Empty)
			{
				query += $" WHERE {condition}";
			}
			if (order != string.Empty)
			{
				query += $" ORDER BY {order}";
			}

			return Select(query);
		}

		public string Insert(string query)
		{
			using var connection = new SqlConnection(GetConnectionString());

			try
			{
				connection.Open();

				using var command = new SqlCommand(query, connection);

				int rowsAffected = command.ExecuteNonQuery();

				return $"{rowsAffected} Rows Affected";
			}
			catch (Exception ex)
			{
				return ex.Message;
			}
		}

		public string Insert(string tableName, IEnumerable<string> columns, IEnumerable<IEnumerable<string>> values)
		{
			var builder = new StringBuilder();
			foreach (var row in values)
			{
				builder.AppendJoin(',', $"({string.Join(',', row)})");
			}
			var query = $"INSERT INTO {tableName} ({string.Join(',', columns)}) VALUES {builder}";

			return Insert(query);
		}
		#endregion

		#region Execute SQL Object
		public DataTable ExecuteStoredProcedure(string procedureName)
		{
			var dataTable = new DataTable();

			using (var connection = new SqlConnection(GetConnectionString()))
			{
				using var command = new SqlCommand(procedureName, connection);
				command.CommandType = CommandType.StoredProcedure;

				using var adapter = new SqlDataAdapter(command);
				connection.Open();
				adapter.Fill(dataTable);
			}

			return dataTable;
		}

		public string Rename(string source, string destination)
		{
			return Execute($"EXEC sp_rename '{source}', '{destination}'");
		}

		#region Transaction
		public string TableTransaction(string tableName, DataTable originalTable, DataTable modifiedTable)
		{
			var table = GetTableInfo(tableName);
			var primaryKeyNames = GetTablePrimaryKeyNames(tableName);
			var columnNames = table.Columns.Select(x => x.Name);
			var nonKeyColumnNames = columnNames.Except(primaryKeyNames);
			var columnNameSequence = string.Join(',', columnNames);
			var sourceColumnNameSequence = string.Join(',', columnNames.Select(x => $"source.{x}"));
			var updateSetNonKeyColumnNames = string.Join(',', nonKeyColumnNames.Select(x => $"target.{x} = source.{x}"));
			//var updateSetCompareNonKeyColumnNames = string.Join(" or ", nonKeyColumnNames.Select(x => $"target.{x} <> source.{x}"));
			var updateSetCompareNonKeyColumnNames = string.Join(" or ", nonKeyColumnNames.Select(x => $"(target.{x} <> source.{x} or (target.{x} is null and source.{x} is not null) or (target.{x} is not null and source.{x} is null))"));
			var targetPrimaryKeyNames = string.Join(" and ", primaryKeyNames.Select(x => $"target.{x} = source.{x}"));
			var mergeQuery = new StringBuilder();
			mergeQuery.AppendLine($"MERGE INTO {tableName} AS target");
			mergeQuery.AppendLine("USING (VALUES");

			for (int i = 0; i < modifiedTable.Rows.Count; i++)
			{
				mergeQuery.Append('(');
				for (int j = 0; j < columnNames.Count(); j++)
				{
					mergeQuery.Append($"@{columnNames.ElementAt(j)}{i}");
					if (j < columnNames.Count() - 1)
						mergeQuery.Append(',');
				}
				mergeQuery.Append(')');
				if (i < modifiedTable.Rows.Count - 1)
					mergeQuery.Append(',');
			}

			mergeQuery.AppendLine($") AS source ({columnNameSequence})");
			mergeQuery.AppendLine($"ON {targetPrimaryKeyNames}");
			mergeQuery.AppendLine($"WHEN MATCHED AND ({updateSetCompareNonKeyColumnNames}) THEN");
			mergeQuery.AppendLine($"UPDATE SET {updateSetNonKeyColumnNames}");
			mergeQuery.AppendLine("WHEN NOT MATCHED BY target THEN");
			mergeQuery.AppendLine($"INSERT ({columnNameSequence})");
			mergeQuery.AppendLine($"VALUES ({sourceColumnNameSequence})");
			mergeQuery.AppendLine("WHEN NOT MATCHED BY source THEN");
			mergeQuery.AppendLine("DELETE;");

			using var connection = new SqlConnection(GetConnectionString());
			connection.Open();
			using var transaction = connection.BeginTransaction();

			try
			{
				using (var cmd = new SqlCommand(mergeQuery.ToString(), connection, transaction))
				{
					for (int i = 0; i < modifiedTable.Rows.Count; i++)
					{
						foreach (var columnName in columnNames)
						{
							var value = modifiedTable.Rows[i][columnName];
							cmd.Parameters.AddWithValue($"@{columnName}{i}", value == DBNull.Value ? DBNull.Value : value);
						}
					}

					cmd.ExecuteNonQuery();
				}

				transaction.Commit();
				return string.Empty;
			}
			catch (Exception ex)
			{
				transaction.Rollback();
				return ex.Message;
			}
		}
		#endregion
		#endregion

		#region Select SQL Object

		#region Database
		public IEnumerable<string> SelectDatabaseNames()
		{
			return Select("name", "sys.databases", "", "name").Field("name");
		}
		#endregion

		#region Table
		public IEnumerable<string> SelectTableNames()
		{
			return Select("table_name", "INFORMATION_SCHEMA.TABLES", "TABLE_TYPE = 'BASE TABLE'", "table_name").Field("table_name");
		}

		public TableInfo GetTableInfo(string tableName)
		{
			using var connection = new SqlConnection(GetConnectionString());
			string query = $"SELECT TABLE_NAME, TABLE_CATALOG, TABLE_SCHEMA, COLUMN_NAME, DATA_TYPE, CHARACTER_MAXIMUM_LENGTH FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = '{tableName}'";
			var command = new SqlCommand(query, connection);
			string query2 = $"SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.KEY_COLUMN_USAGE WHERE OBJECTPROPERTY(OBJECT_ID(CONSTRAINT_SCHEMA + '.' + CONSTRAINT_NAME), 'IsPrimaryKey') = 1 AND TABLE_NAME = '{tableName}'";
			var command2 = new SqlCommand(query2, connection);

			try
			{
				connection.Open();

				var reader = command.ExecuteReader();
				var columns = new List<TableColumnInfo>();
				var _tableName = string.Empty;
				var tableCatalog = string.Empty;
				var tableSchema = string.Empty;
				while (reader.Read())
				{
					_tableName = reader["TABLE_NAME"].ToString() ?? string.Empty;
					tableCatalog = reader["TABLE_CATALOG"].ToString() ?? string.Empty;
					tableSchema = reader["TABLE_SCHEMA"].ToString() ?? string.Empty;

					var columnName = reader["COLUMN_NAME"].ToString() ?? string.Empty;
					var dataType = reader["DATA_TYPE"].ToString() ?? string.Empty;
					var maxLength = reader["CHARACTER_MAXIMUM_LENGTH"].ToString() ?? string.Empty;

					columns.Add(new TableColumnInfo(columnName, dataType, maxLength));
				}
				reader.Close();

				var reader2 = command2.ExecuteReader();
				while (reader2.Read())
				{
					var value = reader2["COLUMN_NAME"].ToString();
					if (string.IsNullOrEmpty(value))
					{
						continue;
					}

					var column = columns.Find(x => x.Name.Equals(value));
					if (column == null)
					{
						continue;
					}

					column.IsKey = true;
				}
				reader2.Close();

				var tableInfo = new TableInfo(_tableName, tableCatalog, tableSchema, columns);

				return tableInfo;
			}
			catch
			{
				throw;
			}
		}

		public IEnumerable<string> GetTablePrimaryKeyNames(string tableName)
		{
			using var connection = new SqlConnection(GetConnectionString());
			string query = $"SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.KEY_COLUMN_USAGE WHERE OBJECTPROPERTY(OBJECT_ID(CONSTRAINT_SCHEMA + '.' + CONSTRAINT_NAME), 'IsPrimaryKey') = 1 AND TABLE_NAME = '{tableName}'";
			var command = new SqlCommand(query, connection);

			try
			{
				connection.Open();
				var reader = command.ExecuteReader();

				List<string> primaryKeys = [];
				while (reader.Read())
				{
					var value = reader["COLUMN_NAME"].ToString();
					if (string.IsNullOrEmpty(value))
					{
						continue;
					}

					primaryKeys.Add(value);

				}
				reader.Close();

				return primaryKeys;
			}
			catch
			{
				throw;
			}
		}
		#endregion

		#region View
		public IEnumerable<string> SelectViewNames()
		{
			return Select("table_name", "INFORMATION_SCHEMA.VIEWS").Field("table_name");
		}
		#endregion

		#region Function
		public IEnumerable<string> SelectFunctionNames()
		{
			return Select("name", "sys.objects", "type IN ('FN', 'IF', 'TF', 'FS', 'FT')", "name").Field("name");
		}
		#endregion

		#region Procedure
		public IEnumerable<string> SelectProcedureNames()
		{
			return Select("routine_name", "INFORMATION_SCHEMA.ROUTINES", "ROUTINE_TYPE = 'PROCEDURE'", "routine_name").Field("routine_name");
		}
		#endregion

		#region Object
		public string GetObject(string objectName)
		{
			using var connection = new SqlConnection(GetConnectionString());
			string query = $"SELECT OBJECT_DEFINITION(OBJECT_ID('{objectName}'))";
			var command = new SqlCommand(query, connection);

			try
			{
				connection.Open();

				var result = command.ExecuteScalar();
				if (result == null || result == DBNull.Value)
				{
					return string.Empty;
				}

				return (string)result;
			}
			catch
			{
				throw;
			}
		}
		#endregion

		#region Etc
		public IEnumerable<string> GetRecentlyModifiedItems(string periodHour)
		{
			using var connection = new SqlConnection(GetConnectionString());
			string query = $"DECLARE @ago DATETIME; SET @ago = DATEADD(HOUR, -{periodHour}, GETDATE()); SELECT 'Table' AS Type, name AS Name, modify_date AS ModDate FROM sys.tables WHERE modify_date >= @ago UNION ALL SELECT 'Procedure' AS Type, name AS Name, modify_date AS ModDate FROM sys.procedures WHERE modify_date >= @ago UNION ALL SELECT 'Function' AS Type, name AS Name, modify_date AS ModDate FROM sys.objects WHERE type_desc = 'SQL_SCALAR_FUNCTION' and modify_date >= @ago ORDER BY Type, Name;";
			var command = new SqlCommand(query, connection);

			try
			{
				connection.Open();
				var reader = command.ExecuteReader();

				var dataTable = new DataTable();
				dataTable.Load(reader);
				reader.Close();

				return dataTable.Field("Name");
			}
			catch
			{
				throw;
			}
		}
		#endregion

		#endregion

		#region Query Generate
		public string GetMergeQuery(string tableName)
		{
			var table = GetTableInfo(tableName);
			var primaryKeyNames = GetTablePrimaryKeyNames(tableName);
			var columnNames = table.Columns.Select(x => x.Name);
			var nonKeyColumnNames = columnNames.Except(primaryKeyNames);
			var columnNameSequence = string.Join(", ", columnNames);
			var columnNameAlphaSequence = string.Join(", ", columnNames.Select(x => $"@{x}"));
			var targetPrimaryKeyNames = string.Join(" and ", primaryKeyNames.Select(x => $"target.{x} = @{x}"));
			var updateSetNonKeyColumnNames = string.Join(", ", nonKeyColumnNames.Select(x => $"{x} = @{x}"));

			var builder = new StringBuilder();
			builder.AppendLine($"MERGE {tableName} AS target");
			builder.AppendLine($"USING ( VALUES ( {columnNameAlphaSequence} ) ) AS source ( {columnNameSequence} )");
			builder.AppendLine($"ON ( {targetPrimaryKeyNames} )");
			builder.AppendLine($"WHEN MATCHED THEN");
			builder.AppendLine($"UPDATE SET");
			builder.AppendLine($"{updateSetNonKeyColumnNames}");
			builder.AppendLine($"WHEN NOT MATCHED THEN");
			builder.AppendLine($"INSERT ( {columnNameSequence} )");
			builder.AppendLine($"VALUES ( {columnNameAlphaSequence} );");

			return builder.ToString();
		}

		public string GetCreateTableQuery(string tableName)
		{
			var table = GetTableInfo(tableName);
			var primaryKeyConstraints = string.Join(',', table.Columns.Where(x => x.IsKey).Select(x => $"{x.Name} ASC"));

			var builder = new StringBuilder();
			builder.AppendLine($"CREATE TABLE {tableName}");
			builder.AppendLine("(");
			foreach (var column in table.Columns)
			{
				builder.AppendLine($"{column.Name} {column.ToTypeString()} {(column.IsKey ? "NOT NULL" : "NULL")},");
			}
			builder.AppendLine(")");
			builder.AppendLine("go");
			builder.AppendLine();
			builder.AppendLine($"ALTER TABLE {tableName}");
			builder.AppendLine($"ADD CONSTRAINT XPK_{tableName} PRIMARY KEY NONCLUSTERED (");
			builder.AppendLine($"{primaryKeyConstraints}");
			builder.AppendLine(")");
			builder.AppendLine("go");

			return builder.ToString();
		}

		public string GetCopyDataQuery(string sourceTableName, string destinationTableName)
		{
			var sourceTable = GetTableInfo(sourceTableName);
			var destinationTable = GetTableInfo(destinationTableName);
			var sourceTableColumnNameSequence = string.Join(',', sourceTable.Columns.Select(x => x.Name));
			var destinationTableColumnNameSequence = string.Join(',', destinationTable.Columns.Select(x => x.Name));

			var builder = new StringBuilder();
			builder.AppendLine($"INSERT INTO {destinationTableName}");
			builder.AppendLine($"({destinationTableColumnNameSequence})");
			builder.AppendLine($"SELECT {sourceTableColumnNameSequence}");
			builder.AppendLine($"FROM {sourceTableName}");

			return builder.ToString();
		}
		#endregion
	}
}
