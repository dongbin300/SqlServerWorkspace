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
				return ex.Message.ToString();
			}
		}

		public DataTable Select(string fieldName, string tableName, string condition = "", string order = "")
		{
			using var connection = new SqlConnection(GetConnectionString());
			var query = $"SELECT {fieldName} FROM {tableName}";
			if (condition != string.Empty)
			{
				query += $" WHERE {condition}";
			}
			if (order != string.Empty)
			{
				query += $" ORDER BY {order}";
			}
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

		public IEnumerable<string> SelectDatabaseNames()
		{
			return Select("name", "sys.databases", "", "name").Field("name");
		}

		public IEnumerable<string> SelectTableNames()
		{
			return Select("table_name", "INFORMATION_SCHEMA.TABLES", "TABLE_TYPE = 'BASE TABLE'", "table_name").Field("table_name");
		}

		public IEnumerable<string> SelectViewNames()
		{
			return Select("table_name", "INFORMATION_SCHEMA.VIEWS").Field("table_name");
		}

		public IEnumerable<string> SelectFunctionNames()
		{
			return Select("name", "sys.objects", "type IN ('FN', 'IF', 'TF', 'FS', 'FT')", "name").Field("name");
		}

		public IEnumerable<string> SelectProcedureNames()
		{
			return Select("routine_name", "INFORMATION_SCHEMA.ROUTINES", "ROUTINE_TYPE = 'PROCEDURE'", "routine_name").Field("routine_name");
		}

		public string GetTableInfo(string tableName)
		{
			using var connection = new SqlConnection(GetConnectionString());
			string query = "SELECT COLUMN_NAME, DATA_TYPE, CHARACTER_MAXIMUM_LENGTH FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = '" + tableName + "'";
			var command = new SqlCommand(query, connection);

			try
			{
				connection.Open();
				var reader = command.ExecuteReader();

				var schemaInfo = new StringBuilder();
				while (reader.Read())
				{
					string columnName = reader["COLUMN_NAME"].ToString();
					string dataType = reader["DATA_TYPE"].ToString();
					string maxLength = reader["CHARACTER_MAXIMUM_LENGTH"].ToString();

					schemaInfo.AppendLine(columnName + ": " + dataType + "(" + maxLength + ")");
				}
				reader.Close();

				return schemaInfo.ToString();
			}
			catch
			{
				throw;
			}
		}

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
	}
}
