namespace Vaetech.Runtime.Html.Compressor
{
	public sealed class HtmlMetrics
	{
		private int filesize = 0;
		private int emptyChars = 0;
		private int inlineScriptSize = 0;
		private int inlineStyleSize = 0;
		private int inlineEventSize = 0;

		/// <summary>Return total filesize of a document, in bytes</summary>		
		public int getFilesize() => filesize;
		public void setFilesize(int filesize) => this.filesize = filesize;

		/// <summary>Returns number of empty characters (spaces, tabs, end of lines) in a document</summary>		
		public int getEmptyChars() => emptyChars;		
		public void setEmptyChars(int emptyChars) => this.emptyChars = emptyChars;

		/// <summary>Return total size of inline <c>&lt;script></c> tags, in bytes</summary>
		public int getInlineScriptSize() => inlineScriptSize;	
		public void setInlineScriptSize(int inlineScriptSize) => this.inlineScriptSize = inlineScriptSize;
		
		/// <summary>Return total size of inline <c>&lt;style></c> tags, in bytes</summary>
		public int getInlineStyleSize() => inlineStyleSize;
		public void setInlineStyleSize(int inlineStyleSize) => this.inlineStyleSize = inlineStyleSize;
	
		/// <summary>Returns total size of inline event handlers (<c>onclick</c>, etc)</summary>
		public int getInlineEventSize() => inlineEventSize;
		public void setInlineEventSize(int inlineEventSize) => this.inlineEventSize = inlineEventSize;

		public override string ToString()
		{
			return string.Format("Filesize={0}, Empty Chars={1}, Script Size={2}, Style Size={3}, Event Handler Size={4}",
				filesize, emptyChars, inlineScriptSize, inlineStyleSize, inlineEventSize);
		}
	}
}
