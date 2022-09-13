namespace stcBot.Models
{
    public class PRIVMSG
    {
        public string source { get; set; }
        public string command { get; set; }
        public string channel { get; set; }
        public string message { get; set; }
        public string[] splitMessage { get; set; }
    }
}
