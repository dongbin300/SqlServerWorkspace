using SqlServerWorkspace.Enums;

using System.Text.RegularExpressions;

namespace SqlServerWorkspace.Extensions
{
	public static partial class EnumExtension
	{
		public static T ParseToEnum<T>(string input) where T : struct, Enum
		{
			string normalizedInput = NormalizeString(input);

			foreach (var value in Enum.GetValues<T>())
			{
				string normalizedEnumName = NormalizeString(value.ToString() ?? string.Empty);
				if (normalizedEnumName == normalizedInput)
				{
					return value;
				}
			}

			throw new ArgumentException($"Cannot convert '{input}' to {typeof(T).Name}");
		}

		private static string NormalizeString(string input)
		{
			return NormalizeRegex().Replace(input, "").ToLower();
		}

		public static AuthenticationType ToAuthenticationType(this string value)
		{
			return ParseToEnum<AuthenticationType>(value);
		}

		public static TreeNodeType ToTreeNodeType(this string value)
		{
			return ParseToEnum<TreeNodeType>(value);
		}

		[GeneratedRegex(@"[\s_]")]
		private static partial Regex NormalizeRegex();
	}
}
