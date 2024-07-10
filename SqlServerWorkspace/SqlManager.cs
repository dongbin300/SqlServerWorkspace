using Microsoft.Data.SqlClient;

using SqlServerWorkspace.Enums;

using System.Data;
using System.Text;

namespace SqlServerWorkspace
{
	public static class SqlManager
	{
		private static AuthenticationType _authenticationType = AuthenticationType.None;

		private static string _server = string.Empty;
		private static string _user = string.Empty;
		private static string _password = string.Empty;
		private static string _database = string.Empty;

		public static void Init(string server, string user, string password)
		{
			_authenticationType = AuthenticationType.SqlServerAuthentication;
			_server = server;
			_user = user;
			_password = password;
		}

		public static void Init(string server, string database)
		{
			_authenticationType = AuthenticationType.WindowsAuthentication;
			_server = server;
			_database = database;
		}

		public static void SetDatabase(string name)
		{
			_database = name;
		}

		public static string GetConnectionString()
		{
			var _connectionString = string.Empty;
			switch (_authenticationType)
			{
				case AuthenticationType.WindowsAuthentication:
					_connectionString = $"Data Source={_server};Initial Catalog={_database};Integrated Security=True;Encrypt=False";
					break;

				case AuthenticationType.SqlServerAuthentication:
					_connectionString = $"Server={_server};User Id={_user};Password={_password};";
					if (_database != string.Empty)
					{
						_connectionString += $"Database={_database};";
					}
					break;

				default:
					break;
			}
			return _connectionString;
		}

		public static string Execute(string query)
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

		public static DataTable Select(string fieldName, string tableName, string condition = "", string order = "")
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

		public static IEnumerable<string> Field(this DataTable dataTable, string fieldName)
		{
			return dataTable.Rows.Cast<DataRow>().Where(row => row[fieldName] != DBNull.Value).Select(row => row[fieldName].ToString());
		}

		public static IEnumerable<string> SelectDatabaseNames()
		{
			return Select("name", "sys.databases", "", "name").Field("name");
		}

		public static IEnumerable<string> SelectTableNames()
		{
			return Select("table_name", "INFORMATION_SCHEMA.TABLES", "TABLE_TYPE = 'BASE TABLE'", "table_name").Field("table_name");
		}

		public static IEnumerable<string> SelectViewNames()
		{
			return Select("table_name", "INFORMATION_SCHEMA.VIEWS").Field("table_name");
		}

		public static IEnumerable<string> SelectFunctionNames()
		{
			return Select("name", "sys.objects", "type IN ('FN', 'IF', 'TF', 'FS', 'FT')", "name").Field("name");
		}

		public static IEnumerable<string> SelectProcedureNames()
		{
			return Select("routine_name", "INFORMATION_SCHEMA.ROUTINES", "ROUTINE_TYPE = 'PROCEDURE'", "routine_name").Field("routine_name");
		}

		public static string GetTableInfo(string tableName)
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

		public static string GetObject(string objectName)
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

		public static IEnumerable<string> GetRecentlyModifiedItems(string periodHour)
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
