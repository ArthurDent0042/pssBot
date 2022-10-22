namespace stcBot.Models
{
	public class Meta
	{
		public string? Poster { get; set; }
		public string? Genres { get; set; }
		public int Current_page { get; set; }
		public int From { get; set; }
		public int Last_page { get; set; }
		public List<Link>? Links { get; set; }
		public string? Path { get; set; }
		public int Per_page { get; set; }
		public int To { get; set; }
		public int Total { get; set; }
	}
}