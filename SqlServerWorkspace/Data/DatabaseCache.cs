using System.Collections.Concurrent;

namespace SqlServerWorkspace.Data
{
	public class DatabaseCache
	{
		private static readonly ConcurrentDictionary<string, CacheItem> _cache = new();
		private static readonly TimeSpan CacheExpiry = TimeSpan.FromMinutes(5);

		private class CacheItem
		{
			public object Data { get; set; } = null!;
			public DateTime CreatedAt { get; set; }
			public string DatabaseName { get; set; } = null!;

			public bool IsExpired => DateTime.Now - CreatedAt > CacheExpiry;
		}

		public static void Set(string key, object data, string databaseName)
		{
			_cache.AddOrUpdate(key,
				new CacheItem { Data = data, CreatedAt = DateTime.Now, DatabaseName = databaseName },
				(k, existing) => new CacheItem { Data = data, CreatedAt = DateTime.Now, DatabaseName = databaseName });
		}

		public static T? Get<T>(string key, string databaseName) where T : class
		{
			if (_cache.TryGetValue(key, out var cacheItem))
			{
				if (cacheItem.DatabaseName == databaseName && !cacheItem.IsExpired)
				{
					return cacheItem.Data as T;
				}
				else
				{
					// 만료된 항목 제거
					_cache.TryRemove(key, out _);
				}
			}
			return null;
		}

		public static void ClearDatabaseCache(string databaseName)
		{
			var keysToRemove = _cache.Where(kvp => kvp.Value.DatabaseName == databaseName)
									.Select(kvp => kvp.Key)
									.ToList();

			foreach (var key in keysToRemove)
			{
				_cache.TryRemove(key, out _);
			}
		}

		public static void ClearAllCache()
		{
			_cache.Clear();
		}

		public static void CleanExpiredItems()
		{
			var keysToRemove = _cache.Where(kvp => kvp.Value.IsExpired)
									.Select(kvp => kvp.Key)
									.ToList();

			foreach (var key in keysToRemove)
			{
				_cache.TryRemove(key, out _);
			}
		}

		public static string GetCacheKey(string databaseName, string type, string? identifier = null)
		{
			return string.IsNullOrEmpty(identifier)
				? $"{databaseName}_{type}"
				: $"{databaseName}_{type}_{identifier}";
		}
	}
}