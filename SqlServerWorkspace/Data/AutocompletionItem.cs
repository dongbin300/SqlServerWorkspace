namespace SqlServerWorkspace.Data
{
	public class AutocompletionItem(string label, int kind, string insertText)
	{
		public string label { get; set; } = label;
		public int kind { get; set; } = kind;
		public string insertText { get; set; } = insertText;
	}
}
