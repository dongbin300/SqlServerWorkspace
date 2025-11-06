namespace SqlServerWorkspace.Data
{
	public class AutocompletionItem
	{
		public string label { get; set; }
		public int kind { get; set; }
		public string insertText { get; set; }
		public string text { get; set; }
		public string detail { get; set; }
		public string documentation { get; set; }

		public AutocompletionItem(string label, int kind, string insertText)
		{
			this.label = label;
			this.kind = kind;
			this.insertText = insertText;
			this.text = insertText; // 기본값으로 insertText 사용
		}

		public AutocompletionItem(string label, int kind, string insertText, string text = null, string detail = null, string documentation = null)
		{
			this.label = label;
			this.kind = kind;
			this.insertText = insertText;
			this.text = text ?? insertText;
			this.detail = detail;
			this.documentation = documentation;
		}
	}
}
