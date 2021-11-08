using System;

namespace Vaetech.Runtime.Html.Compressor
{
	public sealed class HtmlCompressorStatistics
	{
		private HtmlMetrics originalMetrics = new HtmlMetrics();
		private HtmlMetrics compressedMetrics = new HtmlMetrics();
		private long time = 0;
		private int preservedSize = 0;

		/// <summary>
		/// Returns metrics of an uncompressed document		
		/// </summary>
		/// <returns>Metrics of an uncompressed document.</returns>
		public HtmlMetrics getOriginalMetrics()
		{
			return originalMetrics;
		}
		
		/// <param name="originalMetrics">The originalMetrics to set</param>
		public void setOriginalMetrics(HtmlMetrics originalMetrics)
		{
			this.originalMetrics = originalMetrics;
		}

		/// <summary>
		/// Returns metrics of a compressed document.<br/><br/>		
		/// </summary>
		/// <returns>Metrics of a compressed document</returns>
		public HtmlMetrics getCompressedMetrics()
		{
			return compressedMetrics;
		}

		/// <summary>
		/// @param compressedMetrics the compressedMetrics to set
		/// </summary>
		public void setCompressedMetrics(HtmlMetrics compressedMetrics)
		{
			this.compressedMetrics = compressedMetrics;
		}

		/// <summary>
		/// Returns total compression time. 
		/// 
		/// <p>Please note that compression performance varies very significantly depending on whether it was 
		/// a cold run or not (specifics of Java VM), so for accurate real world results it is recommended 
		/// to take measurements accordingly.   
		/// 
		/// @return the compression time, in milliseconds 
		///      
		/// </summary>
		public long getTime()
		{
			return time;
		}

		/// <summary>
		/// @param time the time to set
		/// </summary>
		public void setTime(long time)
		{
			this.time = time;
		}

		/// <summary>
		/// Returns total size of blocks that were skipped by the compressor 
		/// (for example content inside <code>&lt;pre></code> tags or inside   
		/// <c>&lt;script></c> tags with disabled javascript compression)
		/// 
		/// @return the total size of blocks that were skipped by the compressor, in bytes
		/// </summary>
		public int getPreservedSize()
		{
			return preservedSize;
		}

		/// <summary>
		/// @param preservedSize the preservedSize to set
		/// </summary>
		public void setPreservedSize(int preservedSize)
		{
			this.preservedSize = preservedSize;
		}

		public override string ToString()
		{
			return String.Format("Time={0}, Preserved={1}, Original={2}, Compressed={3}", time, preservedSize, originalMetrics, compressedMetrics);
		}
	}
}
