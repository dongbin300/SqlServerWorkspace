﻿using System.Data;

namespace SqlServerWorkspace.Data
{
	public class TableColumnInfo(string name, string type, string length)
	{
		public string Name { get; set; } = name;
		public string Type { get; set; } = type;
		public SqlDbType TrueType => (SqlDbType)Enum.Parse(typeof(SqlDbType), Type);
		public string Length { get; set; } = length;
		public int TrueLength => int.Parse(Length);
	}
}