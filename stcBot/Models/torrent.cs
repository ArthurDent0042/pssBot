namespace aitherBot.Models
{
	public class Attributes
	{
		public Meta? Meta { get; set; }
		public string? Name { get; set; }
		public string? Release_year { get; set; }
		public string? Category { get; set; }
		public string? Type { get; set; }
		public string? Resolution { get; set; }
		public string? Media_info { get; set; }
		public string BD_info { get; set; }
		public string? Description { get; set; }
		public string? Info_hash { get; set; }
		public Int64 Size { get; set; }
		public int Num_file { get; set; }
		public string? Freeleech { get; set; }
		public int Double_upload { get; set; }
		public int @Internal { get; set; }
		public string? Uploader { get; set; }
		public int Seeders { get; set; }
		public int Leechers { get; set; }
		public int Times_completed { get; set; }
		public string? TMDB_id { get; set; }
		public string? IMDB_id { get; set; }
		public string? TVDB_id { get; set; }
		public string? Mal_id { get; set; }
		public string? Igdb_id { get; set; }
		public int Category_id { get; set; }
		public int Type_id { get; set; }
		public int Resolution_id { get; set; }
		public DateTime Created_at { get; set; }
		public string? Download_link { get; set; }
		public string? Details_link { get; set; }
	}

	public class Datum
	{
		public string? Type { get; set; }
		public string? Id { get; set; }
		public Attributes? Attributes { get; set; }
	}

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

	public class Meta
	{
		public string? Poster { get; set; }
		public string	 Genres { get; set; }
		public int Current_page { get; set; }
		public int From { get; set; }
		public int Last_page { get; set; }
		public List<Link?> Links { get; set; }
		public string? Path { get; set; }
		public int Per_page { get; set; }
		public int To { get; set; }
		public int Total { get; set; }
	}

	public class Torrent
	{
		public List<Datum> Data { get; set; }
		public Link? Links { get; set; }
		public Meta? Meta { get; set; }
	}
}
