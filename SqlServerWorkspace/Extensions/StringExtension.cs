namespace SqlServerWorkspace.Extensions
{
	public static class StringExtension
	{
		public static string CombinePath(this string a, string b) => a.EndsWith('/') ? a + b : a + '/' + b;
	}
}
