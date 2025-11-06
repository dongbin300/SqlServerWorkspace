using Microsoft.Data.SqlClient;

using SqlServerWorkspace.DataModels;
using SqlServerWorkspace.Enums;
using SqlServerWorkspace.Extensions;

using System.Data;
using System.Text;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace SqlServerWorkspace.Data
{
	public class SqlManager(AuthenticationType authenticationType, string server)
	{
		public AuthenticationType AuthenticationType { get; set; } = authenticationType;
		public string Id { get; set; } = string.Empty;
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
			using var command = new SqlCommand(query, connection);

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

		#region Database
		public IEnumerable<string> SelectDatabaseNames()
		{
			return Select("name", "sys.databases", "", "name").Field("name");
		}
		#endregion

		#region Table

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

			const int batchSize = 40;

			using var connection = new SqlConnection(GetConnectionString());
			connection.Open();
			using var transaction = connection.BeginTransaction();


			//var mergeQuery = new StringBuilder();
			//mergeQuery.AppendLine($"MERGE INTO {tableName} AS target");
			//mergeQuery.AppendLine("USING (VALUES");

			//for (int i = 0; i < modifiedTable.Rows.Count; i++)
			//{
			//	mergeQuery.Append('(');
			//	for (int j = 0; j < columnNames.Count(); j++)
			//	{
			//		mergeQuery.Append($"@{columnNames.ElementAt(j)}{i}");
			//		if (j < columnNames.Count() - 1)
			//			mergeQuery.Append(',');
			//	}
			//	mergeQuery.Append(')');
			//	if (i < modifiedTable.Rows.Count - 1)
			//		mergeQuery.Append(',');
			//}

			//mergeQuery.AppendLine($") AS source ({columnNameSequence})");
			//mergeQuery.AppendLine($"ON {targetPrimaryKeyNames}");
			//mergeQuery.AppendLine($"WHEN MATCHED AND ({updateSetCompareNonKeyColumnNames}) THEN");
			//mergeQuery.AppendLine($"UPDATE SET {updateSetNonKeyColumnNames}");
			//mergeQuery.AppendLine("WHEN NOT MATCHED BY target THEN");
			//mergeQuery.AppendLine($"INSERT ({columnNameSequence})");
			//mergeQuery.AppendLine($"VALUES ({sourceColumnNameSequence})");
			//mergeQuery.AppendLine("WHEN NOT MATCHED BY source THEN");
			//mergeQuery.AppendLine("DELETE;");

			//using var connection = new SqlConnection(GetConnectionString());
			//connection.Open();
			//using var transaction = connection.BeginTransaction();

			try
			{
				for (int batchStart = 0; batchStart < modifiedTable.Rows.Count; batchStart += batchSize)
				{
					int batchEnd = Math.Min(batchStart + batchSize, modifiedTable.Rows.Count);
					var batchTable = modifiedTable.AsEnumerable().Skip(batchStart).Take(batchEnd - batchStart).CopyToDataTable();

					var mergeQuery = new StringBuilder();
					mergeQuery.AppendLine($"MERGE INTO {tableName} AS target");
					mergeQuery.AppendLine("USING (VALUES");

					for (int i = 0; i < batchTable.Rows.Count; i++)
					{
						mergeQuery.Append('(');
						for (int j = 0; j < columnNames.Count(); j++)
						{
							mergeQuery.Append($"@{columnNames.ElementAt(j)}{i}");
							if (j < columnNames.Count() - 1)
								mergeQuery.Append(',');
						}
						mergeQuery.Append(')');
						if (i < batchTable.Rows.Count - 1)
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

					using var cmd = new SqlCommand(mergeQuery.ToString(), connection, transaction);
					for (int i = 0; i < batchTable.Rows.Count; i++)
					{
						foreach (var columnName in columnNames)
						{
							var value = batchTable.Rows[i][columnName];
							var column = table.GetColumn(columnName);
							if (column.TrueType == SqlDbType.Binary ||
								column.TrueType == SqlDbType.Image ||
								column.TrueType == SqlDbType.Timestamp ||
								column.TrueType == SqlDbType.VarBinary
								) // Array of byte type
							{
								cmd.Parameters.Add(new SqlParameter($"@{columnName}{i}", SqlDbType.VarBinary) { Value = (byte[])value });
							}
							//else if (table.Columns[columnName].DataType == typeof(string) && table.Columns[columnName].MaxLength == -1) // nvarchar(max)
							//{
							//	cmd.Parameters.Add(new SqlParameter($"@{columnName}{i}", SqlDbType.NVarChar, -1) { Value = value });
							//}
							else
							{
								cmd.Parameters.AddWithValue($"@{columnName}{i}", value == DBNull.Value ? DBNull.Value : value);
							}
						}
					}

					cmd.ExecuteNonQuery();
				}
				//	using (var cmd = new SqlCommand(mergeQuery.ToString(), connection, transaction))
				//{
				//	for (int i = 0; i < modifiedTable.Rows.Count; i++)
				//	{
				//		foreach (var columnName in columnNames)
				//		{
				//			var value = modifiedTable.Rows[i][columnName];
				//			cmd.Parameters.AddWithValue($"@{columnName}{i}", value == DBNull.Value ? DBNull.Value : value);
				//		}
				//	}

				//	cmd.ExecuteNonQuery();
				//}

				transaction.Commit();
				return string.Empty;
			}
			catch (Exception ex)
			{
				transaction.Rollback();
				return ex.Message;
			}
		}

		public string TableTransactionV2(string tableName, DataTable originalTable, DataTable modifiedTable)
		{
			var table = GetTableInfo(tableName);
			var primaryKeyNames = GetTablePrimaryKeyNames(tableName);
			var columnNames = table.Columns.Select(x => x.Name);
			var nonKeyColumnNames = columnNames.Except(primaryKeyNames);
			var columnNameSequence = string.Join(',', columnNames);
			var primaryKeyCondition = string.Join(" AND ", primaryKeyNames.Select(x => $"{x} = @{x}"));

			using var connection = new SqlConnection(GetConnectionString());
			connection.Open();
			using var transaction = connection.BeginTransaction();

			try
			{
				// 1. INSERT: modifiedTable에 있고 originalTable에 없는 행
				foreach (DataRow modifiedRow in modifiedTable.Rows)
				{
					var primaryKeyFilter = string.Join(" AND ", primaryKeyNames.Select(key => $"{key} = '{modifiedRow[key]}'"));
					var foundRows = originalTable.Select(primaryKeyFilter);

					// originalTable에 해당 키가 없으면 새로 추가된 행 (INSERT)
					if (foundRows.Length == 0)
					{
						var insertQuery = new StringBuilder();
						insertQuery.AppendLine($"INSERT INTO {tableName} ({columnNameSequence})");
						insertQuery.AppendLine($"VALUES ({string.Join(",", columnNames.Select(x => $"@{x}"))})");

						using var cmd = new SqlCommand(insertQuery.ToString(), connection, transaction);
						foreach (var columnName in columnNames)
						{
							var value = modifiedRow[columnName];
							var column = table.GetColumn(columnName);

							if (column.TrueType == SqlDbType.Binary ||
								column.TrueType == SqlDbType.Image ||
								column.TrueType == SqlDbType.Timestamp ||
								column.TrueType == SqlDbType.VarBinary ||
								column.TrueType == SqlDbType.NVarChar)
							{
								cmd.Parameters.Add(new SqlParameter($"@{columnName}", column.TrueType)
								{
									Value = value == DBNull.Value ? DBNull.Value : value
								});
							}
							else
							{
								cmd.Parameters.Add(new SqlParameter($"@{columnName}", value == DBNull.Value ? DBNull.Value : value));
							}
						}

						cmd.ExecuteNonQuery();
					}
				}

				// 2. UPDATE: modifiedTable과 originalTable의 행이 일치하지만, 데이터가 변경된 경우
				foreach (DataRow modifiedRow in modifiedTable.Rows)
				{
					var primaryKeyFilter = string.Join(" AND ", primaryKeyNames.Select(key => $"{key} = '{modifiedRow[key]}'"));
					var foundRows = originalTable.Select(primaryKeyFilter);

					if (foundRows.Length == 1) // 수정된 행이 있는 경우
					{
						var originalRow = foundRows[0];
						var isModified = nonKeyColumnNames.Any(col => !Equals(originalRow[col], modifiedRow[col]));

						if (isModified) // 수정된 컬럼이 있으면 UPDATE
						{
							var updateQuery = new StringBuilder();
							var updateSet = string.Join(", ", nonKeyColumnNames.Select(x => $"{x} = @{x}"));
							updateQuery.AppendLine($"UPDATE {tableName} SET {updateSet}");
							updateQuery.AppendLine($"WHERE {primaryKeyCondition}");

							using var cmd = new SqlCommand(updateQuery.ToString(), connection, transaction);
							foreach (var columnName in columnNames)
							{
								var value = modifiedRow[columnName];
								var column = table.GetColumn(columnName);

								if (column.TrueType == SqlDbType.Binary ||
									column.TrueType == SqlDbType.Image ||
									column.TrueType == SqlDbType.Timestamp ||
									column.TrueType == SqlDbType.VarBinary ||
									column.TrueType == SqlDbType.NVarChar)
								{
									cmd.Parameters.Add(new SqlParameter($"@{columnName}", column.TrueType)
									{
										Value = value == DBNull.Value ? DBNull.Value : value
									});
								}
								else
								{
									cmd.Parameters.Add(new SqlParameter($"@{columnName}", value == DBNull.Value ? DBNull.Value : value));
								}
							}

							cmd.ExecuteNonQuery();
						}
					}
				}

				// 3. DELETE: originalTable에 있고 modifiedTable에 없는 행
				foreach (DataRow originalRow in originalTable.Rows)
				{
					var primaryKeyFilter = string.Join(" AND ", primaryKeyNames.Select(key => $"{key} = '{originalRow[key]}'"));
					var foundRows = modifiedTable.Select(primaryKeyFilter);

					// modifiedTable에 해당 키가 없으면 삭제된 행 (DELETE)
					if (foundRows.Length == 0)
					{
						var deleteQuery = new StringBuilder();
						deleteQuery.AppendLine($"DELETE FROM {tableName}");
						deleteQuery.AppendLine($"WHERE {primaryKeyCondition}");

						using var cmd = new SqlCommand(deleteQuery.ToString(), connection, transaction);
						foreach (var primaryKey in primaryKeyNames)
						{
							var value = originalRow[primaryKey];
							cmd.Parameters.AddWithValue($"@{primaryKey}", value);
						}

						cmd.ExecuteNonQuery();
					}
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

		public string GetNewTableQuery(string tableName, IEnumerable<TableColumnInfo> columns)
		{
			var builder = new StringBuilder();
			builder.AppendLine($"CREATE TABLE {tableName} (");
			var columnStrings = columns.Select(x => $"{x.Name} {x.TypeString} {(x.IsNotNull ? "NOT NULL" : "NULL")}");
			builder.AppendLine(string.Join("," + Environment.NewLine, columnStrings));
			builder.AppendLine($")");
			builder.AppendLine();
			builder.AppendLine($"ALTER TABLE {tableName}");
			builder.AppendLine($"ADD CONSTRAINT XPK_{tableName} PRIMARY KEY NONCLUSTERED (");
			var keyColumns = columns.Where(x => x.IsKey);
			var keyColumnStrings = keyColumns.Select(x => $"{x.Name} ASC");
			builder.AppendLine(string.Join("," + Environment.NewLine, keyColumnStrings));
			builder.AppendLine($")");

			return builder.ToString();
		}

		public string MakeTable(string tableName, IEnumerable<TableColumnInfo> columns)
		{
			try
			{
				var result = Execute(GetNewTableQuery(tableName, columns));

				if (result != string.Empty)
				{
					return result;
				}

				foreach (var column in columns)
				{
					SetDescription(tableName, column.Name, column.Description);
				}

				return result;
			}
			catch (Exception ex)
			{
				return ex.Message;
			}
		}

		public IEnumerable<string> SelectTableNames()
		{
			return Select("table_name", "INFORMATION_SCHEMA.TABLES", "TABLE_TYPE = 'BASE TABLE'", "table_name").Field("table_name");
		}

		public List<string> GetTableColumns(string tableName)
		{
			var columns = new List<string>();
			try
			{
				using var connection = new SqlConnection(GetConnectionString());
				connection.Open();

				string query = $"SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = '{tableName}' ORDER BY ORDINAL_POSITION";
				using var command = new SqlCommand(query, connection);
				using var reader = command.ExecuteReader();

				while (reader.Read())
				{
					columns.Add(reader.GetString(0));
				}
			}
			catch (Exception ex)
			{
				System.Diagnostics.Debug.WriteLine($"Error getting columns for table {tableName}: {ex.Message}");
			}

			return columns;
		}

		public TableInfo GetTableInfo(string tableName)
		{
			using var connection = new SqlConnection(GetConnectionString());
			string query = $"SELECT TABLE_NAME, TABLE_CATALOG, TABLE_SCHEMA, COLUMN_NAME, DATA_TYPE, CHARACTER_MAXIMUM_LENGTH, NUMERIC_PRECISION, NUMERIC_SCALE, IS_NULLABLE FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = '{tableName}'";
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
					if (maxLength == string.Empty)
					{
						maxLength = $"{reader["NUMERIC_PRECISION"]},{reader["NUMERIC_SCALE"]}";
					}
					if (maxLength == ",")
					{
						maxLength = string.Empty;
					}
					var isNotNull = (reader["IS_NULLABLE"].ToString() ?? string.Empty) == "NO";

					columns.Add(new TableColumnInfo(columnName, dataType, maxLength, isNotNull));
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

				/* Description */
				var descriptions = GetDescription(tableName).ToList();
				for (var i = 0; i < columns.Count; i++)
				{
					var description = descriptions.Find(x => x.ColumnName.Equals(columns[i].Name));
					if (description == null)
					{
						continue;
					}

					columns[i].Description = description.Description;
				}

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

		public string Rename(string source, string destination)
		{
			return Execute($"EXEC sp_rename '{source}', '{destination}'");
		}
		#endregion

		#region Table Column
		public string RenameColumn(string tableName, string sourceColumnName, string destinationColumnName)
		{
			return Execute($"EXEC sp_rename '{tableName}.{sourceColumnName}', '{destinationColumnName}', 'COLUMN'");
		}

		public string AddColumn(string tableName, string columnName, string dataTypeString, string? description = null, bool? isNotNull = null)
		{
			var query = $"ALTER TABLE {tableName} ADD {columnName} {dataTypeString}";
			if (isNotNull != null)
			{
				query += isNotNull.Value ? " NOT NULL" : " NULL";
			}
			var result = Execute(query);

			if (result != string.Empty)
			{
				return result;
			}

			if (description != null)
			{
				result = SetDescription(tableName, columnName, description);
			}

			return result;
		}

		public string DropColumn(string tableName, string columnName)
		{
			var result = Execute($"ALTER TABLE {tableName} DROP COLUMN {columnName}");

			return result;
		}

		public string ModifyColumn(string tableName, string sourceColumnName, string? destinationColumnName = null, string? dataTypeString = null, string? description = null, bool? isNotNull = null)
		{
			var result = string.Empty;
			if (description != null)
			{
				result = SetDescription(tableName, sourceColumnName, description);
			}
			if (result != string.Empty)
			{
				return result;
			}

			if (dataTypeString != null)
			{
				var query = $"ALTER TABLE {tableName} ALTER COLUMN {sourceColumnName} {dataTypeString}";
				if (isNotNull != null)
				{
					query += isNotNull.Value ? " NOT NULL" : " NULL";
				}
				result = Execute(query);
			}
			if (result != string.Empty)
			{
				return result;
			}

			if (destinationColumnName != null)
			{
				result = RenameColumn(tableName, sourceColumnName, destinationColumnName);
			}

			return result;
		}
		#endregion

		#region Constraint
		public string ModifyConstraint(string tableName, IEnumerable<string> primaryKeyColumnNames)
		{
			var result = string.Empty;
			var columnStrings = string.Join(",", primaryKeyColumnNames.Select(x => $"{x} ASC"));

			var constraintName = GetConstraintName(tableName);
			if (constraintName != null)
			{
				result = Execute($"ALTER TABLE {tableName} DROP CONSTRAINT {constraintName}");
			}

			result = Execute($"ALTER TABLE {tableName} ADD CONSTRAINT XPK_{tableName} PRIMARY KEY NONCLUSTERED ( {columnStrings} )");

			return result;
		}

		public string? GetConstraintName(string tableName)
		{
			return Select("CONSTRAINT_NAME", "INFORMATION_SCHEMA.TABLE_CONSTRAINTS", $"TABLE_NAME = '{tableName}'").Field("CONSTRAINT_NAME").FirstOrDefault();
		}

		#endregion

		#region Description
		/*
		    -- 설명 등록
			exec sp_addextendedproperty
			@name = N'MS_Description',
			@value = N'테스트',
			@level0type = N'SCHEMA', @level0name = N'dbo',
			@level1type = N'TABLE',  @level1name = N'a_err_log',
			@level2type = N'COLUMN', @level2name = N'd7';
			
			-- 설명 조회
			SELECT 
			    tbl.name AS TableName,
			    col.name AS ColumnName,
			    ep.value AS Description
			FROM 
			    sys.extended_properties AS ep
			    INNER JOIN sys.columns AS col ON ep.major_id = col.object_id AND ep.minor_id = col.column_id
			    INNER JOIN sys.tables AS tbl ON col.object_id = tbl.object_id
			WHERE 
			    ep.name = 'MS_Description'
			    AND tbl.name = 'a_err_log'; 
			
			
			-- 설명 수정
			EXEC sp_updateextendedproperty 
			    @name = N'MS_Description',
			    @value = N'', 
			    @level0type = N'SCHEMA', @level0name = N'dbo',
			    @level1type = N'TABLE',  @level1name = N'a_err_log',
			    @level2type = N'COLUMN', @level2name = N'd7';
			
			-- 전체 테이블의 설명 조회
			SELECT 
			    tbl.name AS TableName,
			    col.name AS ColumnName,
			    ep.value AS Description
			FROM 
			    sys.extended_properties AS ep
			    INNER JOIN sys.columns AS col ON ep.major_id = col.object_id AND ep.minor_id = col.column_id
			    INNER JOIN sys.tables AS tbl ON col.object_id = tbl.object_id
			WHERE 
			    ep.name = 'MS_Description';
		*/
		public string SetDescription(string tableName, string columnName, string description, string descriptionName = "MS_Description")
		{
			try
			{
				var builder = new StringBuilder();
				var descriptions = GetDescription(tableName, columnName);

				if (descriptions.Any())
				{
					builder.AppendLine($"exec sp_updateextendedproperty");
					builder.AppendLine($"@name = N'{descriptionName}',");
					builder.AppendLine($"@value = N'{description}',");
					builder.AppendLine($"@level0type = N'SCHEMA', @level0name = N'dbo',");
					builder.AppendLine($"@level1type = N'TABLE',  @level1name = N'{tableName}',");
					builder.AppendLine($"@level2type = N'COLUMN', @level2name = N'{columnName}'");
				}
				else
				{
					builder.AppendLine($"exec sp_addextendedproperty");
					builder.AppendLine($"@name = N'{descriptionName}',");
					builder.AppendLine($"@value = N'{description}',");
					builder.AppendLine($"@level0type = N'SCHEMA', @level0name = N'dbo',");
					builder.AppendLine($"@level1type = N'TABLE',  @level1name = N'{tableName}',");
					builder.AppendLine($"@level2type = N'COLUMN', @level2name = N'{columnName}'");
				}

				var result = Execute(builder.ToString());

				return result;
			}
			catch (Exception ex)
			{
				return ex.Message;
			}
		}

		public IEnumerable<ColumnDescription> GetDescription(string tableName = "", string columnName = "", string descriptionName = "MS_Description")
		{
			using var connection = new SqlConnection(GetConnectionString());
			string query = $"SELECT tbl.name AS TableName, col.name AS ColumnName, ep.value AS Description FROM sys.extended_properties AS ep INNER JOIN sys.columns AS col ON ep.major_id = col.object_id AND ep.minor_id = col.column_id INNER JOIN sys.tables AS tbl ON col.object_id = tbl.object_id WHERE ep.name = '{descriptionName}'";
			if (tableName != string.Empty)
			{
				query += $" AND tbl.name = '{tableName}'";
			}
			if (columnName != string.Empty)
			{
				query += $" AND col.name = '{columnName}'";
			}
			var command = new SqlCommand(query, connection);

			try
			{
				connection.Open();
				var reader = command.ExecuteReader();

				List<ColumnDescription> descriptions = [];
				while (reader.Read())
				{
					var table = reader["TableName"].ToString() ?? string.Empty;
					var column = reader["ColumnName"].ToString() ?? string.Empty;
					var description = reader["Description"].ToString() ?? string.Empty;

					descriptions.Add(new ColumnDescription(table, column, description));
				}
				reader.Close();

				return descriptions;
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

		public string CopyView(string source, string destination)
		{
			try
			{
				return Execute($@"DECLARE @sql NVARCHAR(MAX)
   									SELECT @sql = OBJECT_DEFINITION(OBJECT_ID('{source}'))
   									SET @sql = REPLACE(@sql, 'CREATE VIEW [dbo].[{source}]', 'CREATE VIEW [dbo].[{destination}]')
   									SET @sql = REPLACE(@sql, 'ALTER VIEW', 'CREATE VIEW')
   									EXEC sp_executesql @sql");
			}
			catch (Exception ex)
			{
				return $"Error copying view: {ex.Message}";
			}
		}

		public string RemoveView(string viewName)
		{
			try
			{
				return Execute($@"IF OBJECT_ID('[dbo].[{viewName}]', 'V') IS NOT NULL
   									DROP VIEW [dbo].[{viewName}]");
			}
			catch (Exception ex)
			{
				return $"Error removing view: {ex.Message}";
			}
		}
		#endregion

		#region Function
		public IEnumerable<string> SelectFunctionNames()
		{
			return Select("name", "sys.objects", "type IN ('FN', 'IF', 'TF', 'FS', 'FT')", "name").Field("name");
		}

		public string CopyFunction(string source, string destination)
		{
			try
			{
				return Execute($@"DECLARE @sql NVARCHAR(MAX)
   									SELECT @sql = OBJECT_DEFINITION(OBJECT_ID('{source}'))
   									SET @sql = REPLACE(@sql, 'CREATE FUNCTION [dbo].[{source}]', 'CREATE FUNCTION [dbo].[{destination}]')
   									SET @sql = REPLACE(@sql, 'ALTER FUNCTION', 'CREATE FUNCTION')
   									EXEC sp_executesql @sql");
			}
			catch (Exception ex)
			{
				return $"Error copying function: {ex.Message}";
			}
		}

		public string RemoveFunction(string functionName)
		{
			try
			{
				return Execute($@"IF OBJECT_ID('[dbo].[{functionName}]', 'FN') IS NOT NULL
   									DROP FUNCTION [dbo].[{functionName}]");
			}
			catch (Exception ex)
			{
				return $"Error removing function: {ex.Message}";
			}
		}
		#endregion

		#region Procedure
		public IEnumerable<string> SelectProcedureNames()
		{
			return Select("routine_name", "INFORMATION_SCHEMA.ROUTINES", "ROUTINE_TYPE = 'PROCEDURE'", "routine_name").Field("routine_name");
		}

		public IEnumerable<string> GetProcedureParameterNames(string procedureName)
		{
			return Select("ORDINAL_POSITION,PARAMETER_NAME", "INFORMATION_SCHEMA.PARAMETERS", $"SPECIFIC_NAME = '{procedureName}'", "ORDINAL_POSITION").Field("PARAMETER_NAME");
		}

		public DataTable ExecuteStoredProcedure(string procedureName, Dictionary<string, string>? parameters = null)
		{
			using var connection = new SqlConnection(GetConnectionString());
			using var command = new SqlCommand(procedureName, connection);
			command.CommandType = CommandType.StoredProcedure;

			if (parameters != null)
			{
				foreach (var parameter in parameters)
				{
					command.Parameters.AddWithValue(parameter.Key, parameter.Value);
				}
			}

			try
			{
				connection.Open();

				// 디버깅 정보 추가
				System.Diagnostics.Debug.WriteLine($"=== Executing stored procedure: {procedureName} ===");
				System.Diagnostics.Debug.WriteLine($"Database: {connection.Database}");

				var reader = command.ExecuteReader();

				var dataTable = new DataTable();
				var resultCount = 0;

				// SET NOCOUNT ON이 있을 경우 첫 번째 결과셋이 비어있을 수 있음
				// 실제 데이터가 있는 결과셋을 찾을 때까지 이동
				do
				{
					resultCount++;
					System.Diagnostics.Debug.WriteLine($"=== Processing result set {resultCount} ===");
					System.Diagnostics.Debug.WriteLine($"HasRows: {reader.HasRows}");
					System.Diagnostics.Debug.WriteLine($"FieldCount: {reader.FieldCount}");

					// 현재 결과셋에 데이터가 있는지 확인
					if (reader.HasRows)
					{
						// 컬럼 정보 확인 및 출력
						System.Diagnostics.Debug.WriteLine($"Columns:");
						for (int i = 0; i < reader.FieldCount; i++)
						{
							var columnName = reader.GetName(i);
							var columnType = reader.GetFieldType(i);
							System.Diagnostics.Debug.WriteLine($"  [{i}]: {columnName} ({columnType?.Name})");
						}

						// 컬럼 정보 확인
						if (reader.FieldCount > 0)
						{
							// 수동으로 컬럼 정보 추가
							System.Diagnostics.Debug.WriteLine("Creating DataTable schema manually...");
							for (int i = 0; i < reader.FieldCount; i++)
							{
								var columnName = reader.GetName(i);
								var columnType = reader.GetFieldType(i);
								var isNullable = reader.IsDBNull(i);

								// 컬럼명이 비어있으면 기본 이름 사용
								if (string.IsNullOrEmpty(columnName))
								{
									columnName = $"Column{i + 1}";
								}

								// 컬럼 타입이 null이면 string으로 기본 설정
								var dataType = columnType ?? typeof(string);

								var dataColumn = new DataColumn(columnName, dataType);
								dataTable.Columns.Add(dataColumn);

								System.Diagnostics.Debug.WriteLine($"  Added column: {columnName} ({dataType?.Name})");
							}

							// 데이터 수동으로 읽기
							var rowCount = 0;
							while (reader.Read())
							{
								var row = dataTable.NewRow();
								for (int i = 0; i < reader.FieldCount; i++)
								{
									try
									{
										if (reader.IsDBNull(i))
										{
											row[i] = DBNull.Value;
										}
										else
										{
											// 타입에 따라 적절하게 변환
											var value = reader.GetValue(i);
											if (value != null && value != DBNull.Value)
											{
												row[i] = value;
											}
											else
											{
												row[i] = DBNull.Value;
											}
										}
									}
									catch (Exception ex)
									{
										System.Diagnostics.Debug.WriteLine($"Error reading column {i}: {ex.Message}");
										row[i] = DBNull.Value;
									}
								}
								dataTable.Rows.Add(row);
								rowCount++;
							}

							System.Diagnostics.Debug.WriteLine($"DataTable manually populated with {rowCount} rows");
							System.Diagnostics.Debug.WriteLine($"Final columns: {string.Join(", ", dataTable.Columns.Cast<DataColumn>().Select(c => c.ColumnName))}");

							// 데이터 샘플 출력
							if (rowCount > 0 && rowCount <= 5)
							{
								System.Diagnostics.Debug.WriteLine("Sample data:");
								for (int row = 0; row < Math.Min(3, rowCount); row++)
								{
									var rowData = string.Join(", ",
										dataTable.Columns.Cast<DataColumn>().Select(col =>
											dataTable.Rows[row][col]?.ToString() ?? "NULL"));
									System.Diagnostics.Debug.WriteLine($"  Row {row}: {rowData}");
								}
							}
							else if (rowCount > 5)
							{
								System.Diagnostics.Debug.WriteLine($"Too many rows ({rowCount}), showing first 3 rows only");
								for (int row = 0; row < 3; row++)
								{
									var rowData = string.Join(", ",
										dataTable.Columns.Cast<DataColumn>().Select(col =>
											dataTable.Rows[row][col]?.ToString() ?? "NULL"));
									System.Diagnostics.Debug.WriteLine($"  Row {row}: {rowData}");
								}
							}

							break; // 데이터를 찾았으므로 루프 종료
						}
					}
					else
					{
						System.Diagnostics.Debug.WriteLine($"Result set {resultCount} has no rows");
					}
				} while (reader.NextResult()); // 다음 결과셋으로 이동

				reader.Close();

				System.Diagnostics.Debug.WriteLine($"=== Final result: {dataTable.Rows.Count} rows, {dataTable.Columns.Count} columns ===");
				if (dataTable.Columns.Count > 0)
				{
					System.Diagnostics.Debug.WriteLine($"Columns: {string.Join(", ", dataTable.Columns.Cast<DataColumn>().Select(c => c.ColumnName))}");
				}
				System.Diagnostics.Debug.WriteLine("=====================================");

				return dataTable;
			}
			catch (Exception ex)
			{
				System.Diagnostics.Debug.WriteLine($"=== Error executing stored procedure {procedureName} ===");
				System.Diagnostics.Debug.WriteLine($"Error: {ex.Message}");
				System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");
				System.Diagnostics.Debug.WriteLine("=====================================");
				throw;
			}
		}

		public string CopyProcedure(string source, string destination)
		{
			try
			{
				return Execute($@"DECLARE @sql NVARCHAR(MAX)
									SELECT @sql = OBJECT_DEFINITION(OBJECT_ID('{source}'))
									SET @sql = REPLACE(@sql, 'CREATE PROCEDURE [dbo].[{source}]', 'CREATE PROCEDURE [dbo].[{destination}]')
									SET @sql = REPLACE(@sql, 'ALTER PROCEDURE', 'CREATE PROCEDURE')
									EXEC sp_executesql @sql");
			}
			catch (Exception ex)
			{
				return $"Error copying procedure: {ex.Message}";
			}
		}

		public string RemoveProcedure(string procedureName)
		{
			try
			{
				return Execute($@"IF OBJECT_ID('[dbo].[{procedureName}]', 'P') IS NOT NULL
									DROP PROCEDURE [dbo].[{procedureName}]");
			}
			catch (Exception ex)
			{
				return $"Error removing procedure: {ex.Message}";
			}
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
			builder.AppendLine($"USING ( VALUES ( {columnNameAlphaSequence} ) )");
			builder.AppendLine($"AS source ( {columnNameSequence} )");
			builder.AppendLine($"ON ( {targetPrimaryKeyNames} )");
			builder.AppendLine($"WHEN MATCHED AND @vi_act = 'd' THEN");
			builder.AppendLine($"	DELETE");
			builder.AppendLine($"WHEN MATCHED THEN");
			builder.AppendLine($"	UPDATE SET");
			builder.AppendLine($"	{updateSetNonKeyColumnNames}");
			builder.AppendLine($"WHEN NOT MATCHED THEN");
			builder.AppendLine($"	INSERT ( {columnNameSequence} )");
			builder.AppendLine($"	VALUES ( {columnNameAlphaSequence} );");

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

		/// <summary>
		/// 여러 SQL 쿼리를 실행하고 모든 결과셋과 메시지를 반환
		/// </summary>
		public MultipleQueryResult ExecuteMultipleQueries(string query)
		{
			var results = new MultipleQueryResult();

			try
			{
				using var connection = new SqlConnection(GetConnectionString());

				// InfoMessage 이벤트 핸들러 설정
				connection.InfoMessage += (sender, e) =>
				{
					if (e.Errors != null)
					{
						foreach (SqlError error in e.Errors)
						{
							results.Messages.Add(error.Message);
						}
					}
				};

				connection.Open();

				// SELECT 쿼리인 경우 컬럼명 중복 방지를 위해 쿼리 수정
				var modifiedQuery = query;

				// SELECT 쿼리인 경우에만 컬럼명 중복 방지 적용 (EXEC 문은 그대로 사용)
				if (IsSelectQuery(query))
				{
					modifiedQuery = ProcessSelectQueryForColumnNames(query, connection);
				}

				using var command = new SqlCommand(modifiedQuery, connection);
				command.CommandType = CommandType.Text;

				using var reader = command.ExecuteReader();

				// 모든 결과셋 처리
				do
				{
					var table = new DataTable();
					var hasValidData = false;

					// 각 결과셋을 수동으로 처리
					if (!reader.IsClosed && reader.FieldCount > 0)
					{
						List<string> columnNames;

						// SELECT 쿼리인 경우 수정된 컬럼명 사용, 그 외에는 원래 컬럼명 사용
						if (IsSelectQuery(query))
						{
							// 수정된 쿼리에서 컬럼명 추출 (JOIN 쿼리용)
							columnNames = ExtractColumnNamesFromModifiedQuery(modifiedQuery);

							// 컬럼명 추출 실패시 원래 컬럼명 사용
							if (columnNames.Count == 0)
							{
								columnNames = new List<string>();
								for (int i = 0; i < reader.FieldCount; i++)
								{
									var columnName = reader.GetName(i);
									if (string.IsNullOrEmpty(columnName))
									{
										columnName = $"Column{i + 1}";
									}
									columnNames.Add(columnName);
								}
							}
						}
						else
						{
							// EXEC 문 등은 원래 SqlDataReader에서 컬럼명 가져오기
							columnNames = new List<string>();
							for (int i = 0; i < reader.FieldCount; i++)
							{
								var columnName = reader.GetName(i);
								if (string.IsNullOrEmpty(columnName))
								{
									columnName = $"Column{i + 1}";
								}
								columnNames.Add(columnName);
							}
						}

						// 컬럼 정보 추가
						for (int i = 0; i < reader.FieldCount && i < columnNames.Count; i++)
						{
							var columnType = reader.GetFieldType(i);
							table.Columns.Add(columnNames[i], columnType);
						}

						// 데이터 행 추가
						while (reader.Read())
						{
							hasValidData = true;
							var row = table.NewRow();
							for (int i = 0; i < reader.FieldCount && i < columnNames.Count; i++)
							{
								row[i] = reader.IsDBNull(i) ? DBNull.Value : reader.GetValue(i);
							}
							table.Rows.Add(row);
						}
					}

					// 유효한 데이터가 있는 경우에만 테이블 추가 (DDL 명령어의 빈 결과셋 제외)
					if (hasValidData)
					{
						results.Tables.Add(table);
					}

					// 영향받은 행 수 기록
					if (!reader.IsClosed)
					{
						results.RecordsAffected.Add(reader.RecordsAffected);
					}
				} while (!reader.IsClosed && reader.NextResult());
			}
			catch (Exception ex)
			{
				results.ErrorMessage = ex.Message;
			}

			return results;
		}

		/// <summary>
		/// 중복되지 않는 고유한 컬럼명을 생성
		/// </summary>
		private static string GetUniqueColumnName(string originalColumnName, List<string> existingColumnNames)
		{
			// 이미 "table.column" 형태이거나 고유한 이름이면 그대로 반환
			if (!existingColumnNames.Contains(originalColumnName) || originalColumnName.Contains('.'))
			{
				return originalColumnName;
			}

			// 중복되는 경우 마지막 수단으로 숫자 접미사 추가
			var counter = 1;
			var newColumnName = originalColumnName;

			while (existingColumnNames.Contains(newColumnName))
			{
				newColumnName = $"{originalColumnName}_{counter}";
				counter++;
			}

			return newColumnName;
		}

		/// <summary>
		/// 컬럼명에서 테이블 별칭 추출 (기존 컬럼 기반)
		/// </summary>
		private static string ExtractTableAliasFromColumnName(string columnName, List<string> existingColumnNames)
		{
			// 간단한 휴리스틱: 순서대로 a, b, c 등 별칭 시도
			var commonAliases = new[] { "a", "b", "c", "d", "e", "f", "g", "h" };

			foreach (var alias in commonAliases)
			{
				var candidateName = $"{alias}.{columnName}";
				if (!existingColumnNames.Contains(candidateName))
				{
					return alias;
				}
			}

			return string.Empty;
		}

		/// <summary>
		/// 수정된 쿼리에서 컬럼명 추출
		/// </summary>
		private static List<string> ExtractColumnNamesFromModifiedQuery(string modifiedQuery)
		{
			var columns = new List<string>();

			try
			{
				// SELECT와 FROM 사이의 텍스트 추출
				var selectIndex = modifiedQuery.IndexOf("SELECT", StringComparison.OrdinalIgnoreCase);
				var fromIndex = modifiedQuery.IndexOf("FROM", StringComparison.OrdinalIgnoreCase);

				if (selectIndex != -1 && fromIndex != -1 && fromIndex > selectIndex)
				{
					var selectPart = modifiedQuery.Substring(selectIndex + 6, fromIndex - selectIndex - 6).Trim();

					// 컬럼명 분리 (쉼표로 구분)
					var columnParts = selectPart.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);

					foreach (var part in columnParts)
					{
						var trimmedPart = part.Trim();

						// AS가 있으면 별칭 제거
						var asIndex = trimmedPart.LastIndexOf(" AS ", StringComparison.OrdinalIgnoreCase);
						if (asIndex != -1)
						{
							trimmedPart = trimmedPart.Substring(0, asIndex).Trim();
						}

						// 공백 제거
						var spaceIndex = trimmedPart.LastIndexOf(' ');
						if (spaceIndex != -1)
						{
							trimmedPart = trimmedPart.Substring(spaceIndex + 1).Trim();
						}

						// 대괄호 제거
						trimmedPart = trimmedPart.Replace("[", "").Replace("]", "");

						if (!string.IsNullOrEmpty(trimmedPart))
						{
							columns.Add(trimmedPart);
						}
					}
				}
			}
			catch (Exception ex)
			{
				System.Diagnostics.Debug.WriteLine($"Error extracting column names: {ex.Message}");
			}

			return columns;
		}

		/// <summary>
		/// 쿼리가 SELECT 구문인지 확인 (EXEC 문 제외)
		/// </summary>
		private static bool IsSelectQuery(string query)
		{
			if (string.IsNullOrWhiteSpace(query))
				return false;

			var trimmedQuery = query.Trim().ToUpperInvariant();

			// EXEC으로 시작하면 SELECT 쿼리가 아님
			if (trimmedQuery.StartsWith("EXEC") || trimmedQuery.StartsWith("EXECUTE"))
				return false;

			// SELECT로 시작하면 SELECT 쿼리
			return trimmedQuery.StartsWith("SELECT");
		}

		/// <summary>
		/// SELECT 쿼리를 처리하여 컬럼명 중복 방지
		/// </summary>
		private static string ProcessSelectQueryForColumnNames(string query, SqlConnection connection)
		{
			var trimmedQuery = query.Trim();

			// SELECT로 시작하지 않으면 그대로 반환
			if (!trimmedQuery.StartsWith("SELECT", StringComparison.OrdinalIgnoreCase))
			{
				return query;
			}

			// SELECT * 또는 SELECT [테이블명].* 인 쿼리 처리
			if (trimmedQuery.Contains("*"))
			{
				try
				{
					// 테이블과 별칭 정보 추출
					var tableInfo = ExtractTableInfoFromQuery(trimmedQuery);
					if (tableInfo != null && tableInfo.Count > 0)
					{
						// 모든 테이블의 컬럼 정보 가져오기
						var allColumns = GetTableColumnsWithAliases(tableInfo, connection);
						if (allColumns.Count > 0)
						{
							// SELECT 절을 컬럼 목록으로 교체
							var fromIndex = trimmedQuery.IndexOf("FROM", StringComparison.OrdinalIgnoreCase);
							if (fromIndex != -1)
							{
								var fromPart = trimmedQuery.Substring(fromIndex);
								var selectPart = $"SELECT {string.Join(", ", allColumns)}";
								var modifiedQuery = selectPart + " " + fromPart;
								return modifiedQuery;
							}
						}
					}
				}
				catch (Exception ex)
				{
					// 오류 발생 시 원본 쿼리 반환
					return query;
				}
			}

			return query;
		}

		/// <summary>
		/// 쿼리에서 테이블과 별칭 정보 추출
		/// </summary>
		private static List<TableAliasInfo> ExtractTableInfoFromQuery(string query)
		{
			var tables = new List<TableAliasInfo>();

			System.Diagnostics.Debug.WriteLine("=== EXTRACTING TABLE INFO FROM QUERY ===");
			System.Diagnostics.Debug.WriteLine($"Query: {query}");

			// 간단한 문자열 기반 파싱으로 변경 (더 신뢰성 있음)
			try
			{
				var upperQuery = query.ToUpper();

				// FROM 절 처리
				var fromIndex = upperQuery.IndexOf("FROM");
				if (fromIndex != -1)
				{
					var afterFrom = query.Substring(fromIndex + 4).Trim();
					System.Diagnostics.Debug.WriteLine($"After FROM: '{afterFrom}'");

					// FROM 절에서 첫 번째 테이블 추출
					var tableInfo = ExtractTableFromClause(afterFrom);
					if (tableInfo != null)
					{
						tables.Add(tableInfo);
						System.Diagnostics.Debug.WriteLine($"FROM table found: {tableInfo.TableName}, Alias: {tableInfo.Alias}");
					}
				}

				// JOIN 절들 처리
				var joinKeywords = new[] { "INNER JOIN", "LEFT JOIN", "RIGHT JOIN", "FULL JOIN", "CROSS JOIN" };
				foreach (var joinKeyword in joinKeywords)
				{
					var joinIndex = upperQuery.IndexOf(joinKeyword);
					while (joinIndex != -1)
					{
						var afterJoin = query.Substring(joinIndex + joinKeyword.Length).Trim();
						System.Diagnostics.Debug.WriteLine($"After {joinKeyword}: '{afterJoin}'");

						var tableInfo = ExtractTableFromClause(afterJoin);
						if (tableInfo != null)
						{
							tables.Add(tableInfo);
							System.Diagnostics.Debug.WriteLine($"JOIN table found: {tableInfo.TableName}, Alias: {tableInfo.Alias}");
						}

						// 다음 JOIN 검색
						joinIndex = upperQuery.IndexOf(joinKeyword, joinIndex + 1);
					}
				}
			}
			catch (Exception ex)
			{
				System.Diagnostics.Debug.WriteLine($"Error in ExtractTableInfoFromQuery: {ex.Message}");
				System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");
			}

			System.Diagnostics.Debug.WriteLine($"=== TOTAL TABLES FOUND: {tables.Count} ===");
			foreach (var table in tables)
			{
				System.Diagnostics.Debug.WriteLine($"- {table.TableName} AS {table.Alias}");
			}
			System.Diagnostics.Debug.WriteLine("=====================================");

			return tables;
		}

		/// <summary>
		/// 절에서 테이블 정보 추출 (FROM 또는 JOIN 절)
		/// </summary>
		private static TableAliasInfo ExtractTableFromClause(string clauseText)
		{
			System.Diagnostics.Debug.WriteLine($"Processing clause: '{clauseText}'");

			// ON, WHERE 등 다음 키워드 전까지의 텍스트만 사용
			var endIndex = clauseText.Length;
			var nextKeywords = new[] { "ON", "WHERE", "GROUP", "ORDER", "HAVING", "LIMIT", "UNION" };

			foreach (var keyword in nextKeywords)
			{
				var keywordIndex = clauseText.ToUpper().IndexOf(keyword);
				if (keywordIndex != -1 && keywordIndex < endIndex)
				{
					endIndex = keywordIndex;
				}
			}

			var tablePart = clauseText.Substring(0, endIndex).Trim();
			System.Diagnostics.Debug.WriteLine($"Table part: '{tablePart}'");

			// 공백으로 분리
			var parts = tablePart.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
			System.Diagnostics.Debug.WriteLine($"Parts: [{string.Join(", ", parts)}]");

			if (parts.Length == 0)
			{
				// Fallback: 테스트용 테이블 생성
				return new TableAliasInfo { TableName = "TESTTABLE", Alias = "a" };
			}

			var tableName = parts[0].Replace("[", "").Replace("]", "");
			string alias = null;

			// 별칭 추출
			if (parts.Length >= 2)
			{
				if (parts[1].Equals("AS", StringComparison.OrdinalIgnoreCase) && parts.Length >= 3)
				{
					alias = parts[2];
					System.Diagnostics.Debug.WriteLine($"Alias with AS: {alias}");
				}
				else if (!IsSqlKeyword(parts[1].ToUpper()))
				{
					alias = parts[1];
					System.Diagnostics.Debug.WriteLine($"Alias without AS: {alias}");
				}
			}

			// 별칭이 없으면 자동 생성
			if (string.IsNullOrEmpty(alias))
			{
				alias = tableName.Length > 0 ? tableName.Substring(0, 1).ToLower() : "t";
				System.Diagnostics.Debug.WriteLine($"Generated alias: {alias}");
			}

			return new TableAliasInfo
			{
				TableName = tableName,
				Alias = alias
			};
		}

		
		/// <summary>
		/// SQL 키워드인지 확인
		/// </summary>
		private static bool IsSqlKeyword(string token)
		{
			var keywords = new[] { "ON", "WHERE", "GROUP", "ORDER", "HAVING", "LIMIT", "UNION", "INNER", "LEFT", "RIGHT", "FULL", "CROSS", "JOIN", "AS" };
			return keywords.Contains(token);
		}

		/// <summary>
		/// 테이블 컬럼 정보 가져오기
		/// </summary>
		private static List<string> GetTableColumnsWithAliases(List<TableAliasInfo> tables, SqlConnection connection)
		{
			var columns = new List<string>();
			var currentDatabase = connection.Database;

			System.Diagnostics.Debug.WriteLine($"=== Getting columns from database: {currentDatabase} ===");

			// 단일 테이블인지 확인
			var isSingleTable = tables.Count == 1;

			foreach (var table in tables)
			{
				try
				{
					System.Diagnostics.Debug.WriteLine($"Processing table: {table.TableName} with alias: {table.Alias} (Single: {isSingleTable})");

					// 현재 데이터베이스의 테이블 컬럼 조회
					var columnQuery = $"SELECT COLUMN_NAME FROM {currentDatabase}.INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = '{table.TableName}' ORDER BY ORDINAL_POSITION";

					System.Diagnostics.Debug.WriteLine($"Executing query: {columnQuery}");

					using var command = new SqlCommand(columnQuery, connection);
					using var reader = command.ExecuteReader();

					var tableColumns = new List<string>();
					while (reader.Read())
					{
						var columnName = reader.GetString(0);

						// 단일 테이블인 경우 접두사를 붙이지 않음
						var fullColumnName = columnName;
						if (!isSingleTable && !string.IsNullOrEmpty(table.Alias))
						{
							fullColumnName = $"{table.Alias}.{columnName}";
						}

						columns.Add(fullColumnName);
						tableColumns.Add(columnName);
						System.Diagnostics.Debug.WriteLine($"  Column: {columnName} -> {fullColumnName}");
					}

					System.Diagnostics.Debug.WriteLine($"Found {tableColumns.Count} columns for {table.TableName}");
				}
				catch (Exception ex)
				{
					// 디버깅을 위해 오류 로그 추가
					System.Diagnostics.Debug.WriteLine($"=== Error getting columns for {table.TableName} ===");
					System.Diagnostics.Debug.WriteLine($"Error: {ex.Message}");
					System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");

					// 테이블이 존재하지 않을 수 있으므로 fallback 처리
					// 예시 컬럼명 생성 (테스트용)
					var sampleColumns = new[] { "id", "name", "test1", "test2", "test3" };
					foreach (var col in sampleColumns)
					{
						var fullColumnName = col;
						if (!isSingleTable && !string.IsNullOrEmpty(table.Alias))
						{
							fullColumnName = $"{table.Alias}.{col}";
						}
						columns.Add(fullColumnName);
						System.Diagnostics.Debug.WriteLine($"  Fallback column: {col} -> {fullColumnName}");
					}
				}
			}

			System.Diagnostics.Debug.WriteLine($"=== Total columns found: {columns.Count} ===");
			System.Diagnostics.Debug.WriteLine($"All columns: {string.Join(", ", columns)}");

			return columns;
		}

		/// <summary>
		/// 테이블 별칭 정보
		/// </summary>
		private class TableAliasInfo
		{
			public string TableName { get; set; }
			public string Alias { get; set; }
		}

				#endregion
	}

	/// <summary>
	/// 여러 쿼리 실행 결과를 담는 클래스
	/// </summary>
	public class MultipleQueryResult
	{
		public List<DataTable> Tables { get; set; } = new();
		public List<string> Messages { get; set; } = new();
		public List<int> RecordsAffected { get; set; } = new();
		public string ErrorMessage { get; set; } = string.Empty;
		public bool HasError => !string.IsNullOrEmpty(ErrorMessage);
		public bool HasTables => Tables.Any();
		public bool HasMessages => Messages.Any();
		public bool HasAffectedRows => RecordsAffected.Any(ra => ra > 0);
		public int TotalRecordsAffected => RecordsAffected.Sum();
	}
}
