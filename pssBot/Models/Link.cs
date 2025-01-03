namespace pssBot.Models
{
	public class Link
	{
		public string? Url { get; set; }
		public string? Label { get; set; }
		public bool Active { get; set; }
		public string? First { get; set; }
		public string? Last { get; set; }
		public string? Prev { get; set; }
		public string? Next { get; set; }
		public string? Self { get; set; }
	}
}