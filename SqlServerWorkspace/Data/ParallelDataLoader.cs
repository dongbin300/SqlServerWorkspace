using SqlServerWorkspace.DataModels;
using SqlServerWorkspace.Enums;

namespace SqlServerWorkspace.Data
{
	public static class ParallelDataLoader
	{
		/// <summary>
		/// 데이터베이스의 모든 객체를 병렬로 로드
		/// </summary>
		public static async Task<DatabaseObjectsData> LoadDatabaseObjectsAsync(SqlManager manager, string databaseName)
		{
			var tasks = new List<Task>
			{
				LoadTablesAsync(manager, databaseName),
				LoadViewsAsync(manager, databaseName),
				LoadFunctionsAsync(manager, databaseName),
				LoadProceduresAsync(manager, databaseName)
			};

			await Task.WhenAll(tasks);

			return new DatabaseObjectsData
			{
				DatabaseName = databaseName,
				Tables = GetCachedData(databaseName, "tables") ?? new List<string>(),
				Views = GetCachedData(databaseName, "views") ?? new List<string>(),
				Functions = GetCachedData(databaseName, "functions") ?? new List<string>(),
				Procedures = GetCachedData(databaseName, "procedures") ?? new List<string>()
			};
		}

		private static async Task LoadTablesAsync(SqlManager manager, string databaseName)
		{
			var cacheKey = DatabaseCache.GetCacheKey(databaseName, "tables");
			var cached = DatabaseCache.Get<List<string>>(cacheKey, databaseName);

			if (cached == null)
			{
				manager.Database = databaseName;
				var tables = await manager.SelectTableNamesAsync();
				DatabaseCache.Set(cacheKey, tables, databaseName);
			}
		}

		private static async Task LoadViewsAsync(SqlManager manager, string databaseName)
		{
			var cacheKey = DatabaseCache.GetCacheKey(databaseName, "views");
			var cached = DatabaseCache.Get<List<string>>(cacheKey, databaseName);

			if (cached == null)
			{
				manager.Database = databaseName;
				var views = await manager.SelectViewNamesAsync();
				DatabaseCache.Set(cacheKey, views, databaseName);
			}
		}

		private static async Task LoadFunctionsAsync(SqlManager manager, string databaseName)
		{
			var cacheKey = DatabaseCache.GetCacheKey(databaseName, "functions");
			var cached = DatabaseCache.Get<List<string>>(cacheKey, databaseName);

			if (cached == null)
			{
				manager.Database = databaseName;
				var functions = await manager.SelectFunctionNamesAsync();
				DatabaseCache.Set(cacheKey, functions, databaseName);
			}
		}

		private static async Task LoadProceduresAsync(SqlManager manager, string databaseName)
		{
			var cacheKey = DatabaseCache.GetCacheKey(databaseName, "procedures");
			var cached = DatabaseCache.Get<List<string>>(cacheKey, databaseName);

			if (cached == null)
			{
				manager.Database = databaseName;
				var procedures = await manager.SelectProcedureNamesAsync();
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
		public static async Task<List<DatabaseObjectsData>> LoadMultipleDatabasesAsync(List<SqlManager> managers)
		{
			var tasks = managers.Select(manager =>
			{
				var databaseName = manager.Database;
				return LoadDatabaseObjectsAsync(manager, databaseName);
			});

			var results = await Task.WhenAll(tasks);
			return results.ToList();
		}
	}

	public class DatabaseObjectsData
	{
		public string DatabaseName { get; set; } = string.Empty;
		public List<string> Tables { get; set; } = new();
		public List<string> Views { get; set; } = new();
		public List<string> Functions { get; set; } = new();
		public List<string> Procedures { get; set; } = new();
	}
}