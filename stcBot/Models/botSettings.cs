namespace stcBot.Models
{
    public class BotSettings
    {
        public string IrcServer { get; set; } = string.Empty;
        public int IrcPort { get; set; }
        public int MaxRetries { get; set; }
        public string User { get; set; } = string.Empty;
        public string Nick { get; set; } = string.Empty;
        public string NickServPassword { get; set; } = string.Empty;
        public List<string>? ChannelsToJoin { get; set; }
        public int RssCheckFrequency { get; set; }
    }
}
