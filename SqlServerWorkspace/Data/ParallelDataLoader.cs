namespace SqlServerWorkspace.Data
{
	public static class ParallelDataLoader
	{
		/// <summary>
		/// 데이터베이스의 모든 객체를 병렬로 로드
		/// </summary>
		public static DatabaseObjectsData LoadDatabaseObjects(SqlManager manager, string databaseName)
		{
			LoadTables(manager, databaseName);
			LoadViews(manager, databaseName);
			LoadFunctions(manager, databaseName);
			LoadProcedures(manager, databaseName);

			return new DatabaseObjectsData
			{
				DatabaseName = databaseName,
				Tables = GetCachedData(databaseName, "tables") ?? [],
				Views = GetCachedData(databaseName, "views") ?? [],
				Functions = GetCachedData(databaseName, "functions") ?? [],
				Procedures = GetCachedData(databaseName, "procedures") ?? []
			};
		}

		private static void LoadTables(SqlManager manager, string databaseName)
		{
			var cacheKey = DatabaseCache.GetCacheKey(databaseName, "tables");
			var cached = DatabaseCache.Get<List<string>>(cacheKey, databaseName);

			if (cached == null)
			{
				manager.Database = databaseName;
				var tables = manager.SelectTableNames();
				DatabaseCache.Set(cacheKey, tables, databaseName);
			}
		}

		private static void LoadViews(SqlManager manager, string databaseName)
		{
			var cacheKey = DatabaseCache.GetCacheKey(databaseName, "views");
			var cached = DatabaseCache.Get<List<string>>(cacheKey, databaseName);

			if (cached == null)
			{
				manager.Database = databaseName;
				var views = manager.SelectViewNames();
				DatabaseCache.Set(cacheKey, views, databaseName);
			}
		}

		private static void LoadFunctions(SqlManager manager, string databaseName)
		{
			var cacheKey = DatabaseCache.GetCacheKey(databaseName, "functions");
			var cached = DatabaseCache.Get<List<string>>(cacheKey, databaseName);

			if (cached == null)
			{
				manager.Database = databaseName;
				var functions = manager.SelectFunctionNames();
				DatabaseCache.Set(cacheKey, functions, databaseName);
			}
		}

		private static void LoadProcedures(SqlManager manager, string databaseName)
		{
			var cacheKey = DatabaseCache.GetCacheKey(databaseName, "procedures");
			var cached = DatabaseCache.Get<List<string>>(cacheKey, databaseName);

			if (cached == null)
			{
				manager.Database = databaseName;
				var procedures = manager.SelectProcedureNames();
				DatabaseCache.Set(cacheKey, procedures, databaseName);
			}
		}

		private static List<string>? GetCachedData(string databaseName, string type)
		{
			var cacheKey = DatabaseCache.GetCacheKey(databaseName, type);
			return DatabaseCache.Get<List<string>>(cacheKey, databaseName);
		}

		/// <summary>
		/// 여러 데이터베이스를 병렬로 로드
		/// </summary>
		public static List<DatabaseObjectsData> LoadMultipleDatabasesAsync(List<SqlManager> managers)
		{
			var result = new List<DatabaseObjectsData>();
			foreach (var manager in managers)
			{
				var databaseName = manager.Database;
				result.Add(LoadDatabaseObjects(manager, databaseName));
			}

			return result;
		}
	}

	public class DatabaseObjectsData
	{
		public string DatabaseName { get; set; } = string.Empty;
		public List<string> Tables { get; set; } = [];
		public List<string> Views { get; set; } = [];
		public List<string> Functions { get; set; } = [];
		public List<string> Procedures { get; set; } = [];
	}
}