namespace pssBot.Models
{
    public class BotSettings
    {
        public string IrcServer { get; set; } = string.Empty;
        public int IrcPort { get; set; } = 6667;
        public int MaxRetries { get; set; } = 3;
        public string User { get; set; } = string.Empty;
        public string Nick { get; set; } = string.Empty;
        public string NickServPassword { get; set; } = string.Empty;
        public List<string>? ChannelsToJoin { get; set; } = new List<string> { string.Empty };
        public int APICheckFrequency { get; set; } = 60;
    }
}