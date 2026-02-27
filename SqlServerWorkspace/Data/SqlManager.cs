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

		public DataTable Select(string query, params SqlParameter[] parameters)
		{
			var dataTable = new DataTable();

			using var connection = new SqlConnection(GetConnectionString());
			using var command = new SqlCommand(query, connection);

			if (parameters is { Length: > 0 })
			{
				command.Parameters.AddRange(parameters);
			}

			try
			{
				connection.Open();
				using var reader = command.ExecuteReader();
				dataTable.Load(reader);
				return dataTable;
			}
			catch (SqlException ex)
			{
				string msg = $"SQL 오류\nQuery: {query}\nParams: {string.Join(", ", parameters?.Select(p => $"{p.ParameterName}={p.Value ?? "null"}") ?? Array.Empty<string>())}\n{ex.Message}";
				throw new Exception(msg, ex);
			}
			catch (Exception ex)
			{
				throw new Exception($"Select 실패\nQuery: {query}\n{ex.Message}", ex);
			}
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

			var allColumns = table.Columns.ToList();

			var insertTargetColumns = allColumns.Where(x => !x.IsIdentity).ToList();
			var insertColumnNames = string.Join(",", insertTargetColumns.Select(x => x.Name));
			var insertParameterNames = string.Join(",", insertTargetColumns.Select(x => $"@{x.Name}"));

			var nonKeyColumnNames = allColumns.Select(x => x.Name).Except(primaryKeyNames).ToList();
			var primaryKeyCondition = string.Join(" AND ", primaryKeyNames.Select(x => $"{x} = @{x}"));

			using var connection = new SqlConnection(GetConnectionString());
			connection.Open();
			using var transaction = connection.BeginTransaction();

			int insertCount = 0;
			int updateCount = 0;
			int deleteCount = 0;

			try
			{
				// 1. INSERT
				foreach (DataRow modifiedRow in modifiedTable.Rows)
				{
					var primaryKeyFilter = string.Join(" AND ", primaryKeyNames.Select(key => $"{key} = '{modifiedRow[key]}'"));
					var foundRows = originalTable.Select(primaryKeyFilter);

					if (foundRows.Length == 0)
					{
						var insertQuery = new StringBuilder();
						insertQuery.AppendLine($"INSERT INTO {tableName} ({insertColumnNames})");
						insertQuery.AppendLine($"VALUES ({insertParameterNames})");

						using var cmd = new SqlCommand(insertQuery.ToString(), connection, transaction);

						// Identity가 아닌 컬럼만 파라미터 추가
						foreach (var column in insertTargetColumns)
						{
							var value = modifiedRow[column.Name];
							if (column.TrueType == SqlDbType.Binary || column.TrueType == SqlDbType.Image ||
								column.TrueType == SqlDbType.Timestamp || column.TrueType == SqlDbType.VarBinary ||
								column.TrueType == SqlDbType.NVarChar)
							{
								cmd.Parameters.Add(new SqlParameter($"@{column.Name}", column.TrueType) { Value = value ?? DBNull.Value });
							}
							else
							{
								cmd.Parameters.Add(new SqlParameter($"@{column.Name}", value ?? DBNull.Value));
							}
						}

						cmd.ExecuteNonQuery();
						insertCount++;
					}
				}

				// 2. UPDATE (수정 시에는 Identity 컬럼이 WHERE절의 PK로 쓰일 수 있으므로 그대로 둡니다)
				foreach (DataRow modifiedRow in modifiedTable.Rows)
				{
					var primaryKeyFilter = string.Join(" AND ", primaryKeyNames.Select(key => $"{key} = '{modifiedRow[key]}'"));
					var foundRows = originalTable.Select(primaryKeyFilter);

					if (foundRows.Length == 1)
					{
						var originalRow = foundRows[0];
						var isModified = nonKeyColumnNames.Any(col => !Equals(originalRow[col], modifiedRow[col]));

						if (isModified)
						{
							var updateQuery = new StringBuilder();
							var updateSet = string.Join(", ", nonKeyColumnNames.Select(x => $"{x} = @{x}"));
							updateQuery.AppendLine($"UPDATE {tableName} SET {updateSet}");
							updateQuery.AppendLine($"WHERE {primaryKeyCondition}");

							using var cmd = new SqlCommand(updateQuery.ToString(), connection, transaction);

							// UPDATE는 WHERE절 때문에 모든 컬럼 정보를 파라미터로 넘김
							foreach (var column in allColumns)
							{
								var value = modifiedRow[column.Name];
								if (column.TrueType == SqlDbType.Binary || column.TrueType == SqlDbType.Image ||
									column.TrueType == SqlDbType.Timestamp || column.TrueType == SqlDbType.VarBinary ||
									column.TrueType == SqlDbType.NVarChar)
								{
									cmd.Parameters.Add(new SqlParameter($"@{column.Name}", column.TrueType) { Value = value ?? DBNull.Value });
								}
								else
								{
									cmd.Parameters.Add(new SqlParameter($"@{column.Name}", value ?? DBNull.Value));
								}
							}

							cmd.ExecuteNonQuery();
							updateCount++;
						}
					}
				}

				// 3. DELETE (기존과 동일)
				foreach (DataRow originalRow in originalTable.Rows)
				{
					var primaryKeyFilter = string.Join(" AND ", primaryKeyNames.Select(key => $"{key} = '{originalRow[key]}'"));
					var foundRows = modifiedTable.Select(primaryKeyFilter);

					if (foundRows.Length == 0)
					{
						var deleteQuery = new StringBuilder();
						deleteQuery.AppendLine($"DELETE FROM {tableName} WHERE {primaryKeyCondition}");

						using var cmd = new SqlCommand(deleteQuery.ToString(), connection, transaction);
						foreach (var primaryKey in primaryKeyNames)
						{
							cmd.Parameters.AddWithValue($"@{primaryKey}", originalRow[primaryKey]);
						}

						cmd.ExecuteNonQuery();
						deleteCount++;
					}
				}

				transaction.Commit();
				return $"I: {insertCount}, U: {updateCount}, D: {deleteCount}";
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

		public TableInfo GetTableInfo(string tableName)
		{
			using var connection = new SqlConnection(GetConnectionString());

			// COLUMNPROPERTY를 사용하여 Identity 여부를 추가로 조회합니다.
			string query = $@"
        SELECT 
            TABLE_NAME, TABLE_CATALOG, TABLE_SCHEMA, COLUMN_NAME, DATA_TYPE, 
            CHARACTER_MAXIMUM_LENGTH, NUMERIC_PRECISION, NUMERIC_SCALE, IS_NULLABLE,
            COLUMNPROPERTY(OBJECT_ID(TABLE_SCHEMA + '.' + TABLE_NAME), COLUMN_NAME, 'IsIdentity') AS IS_IDENTITY
        FROM INFORMATION_SCHEMA.COLUMNS 
        WHERE TABLE_NAME = '{tableName}'";

			var command = new SqlCommand(query, connection);

			string query2 = $@"
        SELECT COLUMN_NAME 
        FROM INFORMATION_SCHEMA.KEY_COLUMN_USAGE 
        WHERE OBJECTPROPERTY(OBJECT_ID(CONSTRAINT_SCHEMA + '.' + CONSTRAINT_NAME), 'IsPrimaryKey') = 1 
        AND TABLE_NAME = '{tableName}'";

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

					if (string.IsNullOrEmpty(maxLength))
					{
						maxLength = $"{reader["NUMERIC_PRECISION"]},{reader["NUMERIC_SCALE"]}";
					}
					if (maxLength == ",")
					{
						maxLength = string.Empty;
					}

					var isNotNull = (reader["IS_NULLABLE"].ToString() ?? string.Empty) == "NO";

					// IsIdentity 정보 추출 (1이면 true, 0이면 false)
					var isIdentity = Convert.ToInt32(reader["IS_IDENTITY"]) == 1;

					var colInfo = new TableColumnInfo(columnName, dataType, maxLength, isNotNull)
					{
						IsIdentity = isIdentity
					};
					columns.Add(colInfo);
				}
				reader.Close();

				// Primary Key 정보 설정
				var reader2 = command2.ExecuteReader();
				while (reader2.Read())
				{
					var value = reader2["COLUMN_NAME"].ToString();
					if (string.IsNullOrEmpty(value)) continue;

					var column = columns.Find(x => x.Name.Equals(value));
					if (column != null)
					{
						column.IsKey = true;
					}
				}
				reader2.Close();

				/* Description 설정 */
				var descriptions = GetDescription(tableName).ToList();
				foreach (var col in columns)
				{
					var description = descriptions.Find(x => x.ColumnName.Equals(col.Name));
					if (description != null)
					{
						col.Description = description.Description;
					}
				}

				return new TableInfo(_tableName, tableCatalog, tableSchema, columns);
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
		/// <summary>
		/// 테이블의 Primary Key를 완전히 새로 지정합니다. (기존 PK 있으면 삭제 후 생성)
		/// </summary>
		/// <param name="tableName">테이블 이름</param>
		/// <param name="primaryKeyColumnNames">새로운 PK로 사용할 컬럼들</param>
		/// <param name="clustered">클러스터드 여부 (기본값 false)</param>
		/// <returns>성공 시 빈 문자열, 실패 시 오류 메시지</returns>
		public string SetPrimaryKey(string tableName, IEnumerable<string> columnNames, bool clustered = false)
		{
			// 1. 이 테이블(PK가 있는 테이블)이 다른 테이블에 의해 참조되고 있는지 확인 & FK 모두 Drop
			var fkDropResult = DropAllReferencingForeignKeys(tableName);
			if (!string.IsNullOrEmpty(fkDropResult))
			{
				return fkDropResult;
			}

			// 2. 기존 PK Drop
			var dropResult = DropPrimaryKeyIfExists(tableName);
			if (!string.IsNullOrEmpty(dropResult))
			{
				return dropResult;
			}

			// 3. 새 PK가 필요하면 Create
			if (columnNames?.Any() == true)
			{
				var createResult = CreatePrimaryKey(tableName, columnNames, clustered);
				if (!string.IsNullOrEmpty(createResult))
				{
					return createResult;
				}
			}

			// 참고: FK를 다시 살리고 싶다면 별도 로직 필요 (지금은 Drop만 함)
			// → 나중에 데이터 무결성 복구를 원하면 FK 재생성 로직을 추가하세요

			return string.Empty;
		}

		public string? GetConstraintName(string tableName)
		{
			return Select("CONSTRAINT_NAME", "INFORMATION_SCHEMA.TABLE_CONSTRAINTS", $"TABLE_NAME = '{tableName}'").Field("CONSTRAINT_NAME").FirstOrDefault();
		}

		/// <summary>
		/// 테이블의 기존 Primary Key 제약 조건을 삭제합니다. (존재하지 않으면 아무 일도 하지 않음)
		/// </summary>
		/// <returns>성공 시 빈 문자열, 실패 시 오류 메시지</returns>
		public string DropPrimaryKeyIfExists(string tableName)
		{
			try
			{
				// 현재 PK 제약조건 이름 찾기
				var pkName = GetPrimaryKeyConstraintName(tableName);
				if (string.IsNullOrWhiteSpace(pkName))
				{
					return string.Empty; // PK가 애초에 없음 → 성공으로 간주
				}

				string sql = $@"
IF EXISTS (SELECT * FROM sys.key_constraints 
           WHERE name = '{pkName}' 
           AND parent_object_id = OBJECT_ID('{tableName}'))
BEGIN
    ALTER TABLE [{tableName}] DROP CONSTRAINT [{pkName}];
END";

				return Execute(sql);
			}
			catch (Exception ex)
			{
				return $"Drop PK failed: {ex.Message}";
			}
		}

		/// <summary>
		/// 테이블의 Primary Key 제약조건 이름을 반환합니다. 없으면 null 또는 빈 문자열
		/// </summary>
		private string? GetPrimaryKeyConstraintName(string tableName)
		{
			string query = $@"
SELECT name 
FROM sys.key_constraints 
WHERE [type] = 'PK' 
  AND parent_object_id = OBJECT_ID('{tableName}')";

			var dt = Select(query);
			if (dt.Rows.Count == 0) return null;
			return dt.Rows[0]["name"]?.ToString();
		}

		/// <summary>
		/// 테이블에 새로운 Primary Key 제약 조건을 생성합니다.
		/// </summary>
		/// <param name="tableName">테이블 이름</param>
		/// <param name="columns">PK로 사용할 컬럼 이름들 (순서 중요)</param>
		/// <param name="clustered">클러스터드 인덱스로 만들지 여부 (기본값: false = NONCLUSTERED)</param>
		/// <returns>성공 시 빈 문자열, 실패 시 오류 메시지</returns>
		public string CreatePrimaryKey(string tableName, IEnumerable<string> columns, bool clustered = false)
		{
			if (columns == null || !columns.Any())
				return "Primary key에 사용할 컬럼이 없습니다.";

			var columnList = string.Join(", ", columns.Select(c => $"[{c.Trim()}] ASC"));

			string clusteredOption = clustered ? "CLUSTERED" : "NONCLUSTERED";

			string constraintName = $"XPK_{tableName}";

			string sql = $@"
ALTER TABLE [{tableName}]
ADD CONSTRAINT [{constraintName}] PRIMARY KEY {clusteredOption} 
(
    {columnList}
);";

			return Execute(sql);
		}

		/// <summary>
		/// 지정한 테이블을 참조하는 모든 외래키(FK)를 삭제합니다.
		/// 성공 시 빈 문자열 반환, 실패 시 오류 메시지 반환
		/// </summary>
		public string DropAllReferencingForeignKeys(string referencedTableName)
		{
			// 1. 참조하는 FK 목록 가져오기
			string query = @"
SELECT 
    fk.name AS FKName,
    OBJECT_SCHEMA_NAME(fk.parent_object_id) + '.' + OBJECT_NAME(fk.parent_object_id) AS ReferencingTable
FROM sys.foreign_keys fk
WHERE fk.referenced_object_id = OBJECT_ID(@ReferencedTable)";

			DataTable dt;
			try
			{
				dt = Select(query, new SqlParameter("@ReferencedTable", referencedTableName));
			}
			catch (Exception ex)
			{
				return $"FK 목록 조회 실패: {ex.Message}";
			}

			if (dt.Rows.Count == 0)
			{
				return string.Empty;  // 참조하는 FK가 하나도 없음 → 성공
			}

			// 2. 각 FK 삭제 시도
			foreach (DataRow row in dt.Rows)
			{
				string fkName = row["FKName"]?.ToString() ?? "";
				string referencingTable = row["ReferencingTable"]?.ToString() ?? "";

				if (string.IsNullOrWhiteSpace(fkName) || string.IsNullOrWhiteSpace(referencingTable))
				{
					continue;
				}

				string dropSql = $"ALTER TABLE {referencingTable} DROP CONSTRAINT [{fkName}];";

				string result = Execute(dropSql);
				if (!string.IsNullOrEmpty(result))
				{
					return $"FK 삭제 실패\n" +
						   $"테이블: {referencingTable}\n" +
						   $"제약조건: {fkName}\n" +
						   $"오류: {result}";
				}

				// 로그용 (필요하면 나중에 로거로 변경)
				// Console.WriteLine($"Dropped FK: {fkName} from {referencingTable}");
			}

			return string.Empty;
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
			var dataTable = new DataTable();

			using (var connection = new SqlConnection(GetConnectionString()))
			{
				using var command = new SqlCommand(procedureName, connection);
				command.CommandType = CommandType.StoredProcedure;

				if (parameters != null)
				{
					foreach (var parameter in parameters)
					{
						command.Parameters.AddWithValue(parameter.Key, parameter.Value);
					}
				}

				using var adapter = new SqlDataAdapter(command);
				connection.Open();
				adapter.Fill(dataTable);
			}

			return dataTable;
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

				using var command = new SqlCommand(query, connection);
				command.CommandType = CommandType.Text;

				connection.Open();

				using var reader = command.ExecuteReader();

				// 모든 결과셋 처리
				do
				{
					var table = new DataTable();

					if (reader.FieldCount == 0)
					{
						continue;
					}

					// 각 결과셋을 수동으로 처리
					if (!reader.IsClosed && reader.FieldCount > 0)
					{
						// 컬럼 정보 추가
						for (int i = 0; i < reader.FieldCount; i++)
						{
							var columnName = reader.GetName(i);
							var columnType = reader.GetFieldType(i);
							table.Columns.Add(columnName, columnType);
						}

						// 데이터 행 추가
						while (reader.Read())
						{
							var row = table.NewRow();
							for (int i = 0; i < reader.FieldCount; i++)
							{
								row[i] = reader.IsDBNull(i) ? DBNull.Value : reader.GetValue(i);
							}
							table.Rows.Add(row);
						}
					}

					results.Tables.Add(table);
				} while (!reader.IsClosed && reader.NextResult());
			}
			catch (Exception ex)
			{
				results.ErrorMessage = ex.Message;
			}

			return results;
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
		public string ErrorMessage { get; set; } = string.Empty;
		public bool HasError => !string.IsNullOrEmpty(ErrorMessage);
		public bool HasTables => Tables.Any();
		public bool HasMessages => Messages.Any();
	}
}
