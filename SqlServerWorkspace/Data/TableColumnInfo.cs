using System.Data;

namespace SqlServerWorkspace.Data
{
	public class TableColumnInfo
	{
		public TableColumnInfo(string name, string type, string length, bool isNotNull)
		{
			Name = name;
			Type = type;
			Length = length;
			IsNotNull = isNotNull;
		}

		public TableColumnInfo(string name, string typeString, bool isKey, bool isNotNull, string description)
		{
			Name = name;
			Type = string.Empty;
			Length = string.Empty;
			TypeString = typeString;
			IsKey = isKey;
			IsNotNull = isNotNull;
			Description = description;
		}

		public string Name { get; set; }
		public string Type { get; set; }
		public SqlDbType TrueType => (SqlDbType)Enum.Parse(typeof(SqlDbType), Type);
		public string Length { get; set; }
		public int TrueLength => int.Parse(Length);
		public bool IsKey { get; set; } = false;
		public bool IsNotNull { get; set; } = false;
		public string Description { get; set; } = string.Empty;

		/// <summary>
		/// Only For New Table
		/// </summary>
		public string TypeString { get; set; } = string.Empty;

		public string ToTypeString() => string.IsNullOrEmpty(Length) ? Type : $"{Type}({Length})";
	}
}
