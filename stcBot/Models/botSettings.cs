namespace stcBot.Models
{
    public class BotSettings
    {
        public string IrcServer { get; set; }
        public int IrcPort { get; set; }
        public int MaxRetries { get; set; }
        public string User { get; set; }
        public string Nick { get; set; }
        public string NickServPassword { get; set; }
        public List<string> ChannelsToJoin { get; set; }
        public int RssCheckFrequency { get; set; }
    }
}
