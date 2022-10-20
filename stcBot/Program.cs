using Microsoft.Extensions.Configuration;
using NLog;
using aitherBot.Models;
using System.Net.Sockets;
using System.ServiceModel.Syndication;
using System.Timers;
using System.Xml;

namespace aitherBot
{
	public class IRCbot
	{
		private static readonly Logger logger = LogManager.GetCurrentClassLogger();
		static bool OkToCheckRSS = false;
		static string rssFeed = string.Empty;
		static string freeleechRssFeed = string.Empty;
		static string announceChannel = string.Empty;
		static int timeToSleepBetweenAnnouncements = 10;
		static BotSettings? botSettings;
		static string torrentHistoryLogFileName = string.Empty;
		static readonly IConfigurationRoot config = new ConfigurationBuilder()
						.SetBasePath(Directory.GetCurrentDirectory())
						.AddJsonFile(path: "appSettings.json", optional: false, reloadOnChange: true)
						.Build();

		static void Main()
		{
			// get bot settings
			botSettings = new BotSettings()
			{
				IrcServer = config.GetValue<string>("ircBotConfig:IRCServer"),
				IrcPort = config.GetValue<int>("ircBotConfig:IRCPort"),
				MaxRetries = config.GetValue<int>("ircBotConfig:MaxRetries"),
				Nick = config.GetValue<string>("ircBotConfig:botNick"),
				NickServPassword = config.GetValue<string>("ircBotConfig:NickServPassword"),
				ChannelsToJoin = config.GetSection("ircBotConfig:channelsToJoin").Get<List<string>>(),
				RssCheckFrequency = config.GetValue<int>("ircBotConfig:RSSCheckFrequencyInSeconds")
			};
			botSettings.User = $"USER {botSettings.Nick} 0 * :{botSettings.Nick}";

			// Path for RSS Feed
			rssFeed = config.GetValue<string>("RSSFeed");

			// Path for Freeleech RSS Feed
			freeleechRssFeed = config.GetValue<string>("FreeleechRSSFeed");

			// Announce channel
			announceChannel = config.GetValue<string>("announceChannel");

			// name of torrent history logfile
			torrentHistoryLogFileName = config.GetValue<string>("torrentHistoryLogFilename");

			// verify history logfile exists
			if (!File.Exists($@"{AppDomain.CurrentDomain.BaseDirectory}{torrentHistoryLogFileName}"))
			{
				File.Create($@"{AppDomain.CurrentDomain.BaseDirectory}{torrentHistoryLogFileName}").Close();
			}

			// time (in seconds) of how long to wait between making announcements
			timeToSleepBetweenAnnouncements = config.GetValue<int>("timeToSleepBetweenAnnouncements");

			// Get the timer started
			SiteTimer();

			// Let's fire up the bot
			_ = new IRCbot();
			Start();
		}

		public IRCbot() { }

		/// <summary>
		/// Used to fire off actions we want to perform at specific intervals
		/// </summary>
		public static void SiteTimer()
		{
			System.Timers.Timer timer = new(interval: botSettings.RssCheckFrequency * 1000);
			timer.Start();
			timer.Elapsed += HandleTimerElapsed;
		}

		public static void HandleTimerElapsed(object sender, ElapsedEventArgs e)
		{
			OkToCheckRSS = true;
		}

		public static void Start()
		{
			logger.Info("aitherBot app starting");
			int retryCount = botSettings.MaxRetries;
			bool retry = true;
			do
			{
				try
				{
					using TcpClient client = new(botSettings.IrcServer, botSettings.IrcPort);
					using NetworkStream stream = client.GetStream();
					using StreamReader reader = new(stream);
					using StreamWriter writer = new(stream);
					logger.Info($"Connecting to {botSettings.IrcServer}");
					logger.Info($"Assigning NICK of {botSettings.Nick} to bot");
					SendMessageToServer(writer, $"NICK {botSettings.Nick}");
					SendMessageToServer(writer, botSettings.User);
					//SendMessageToServer(writer, $"TOPIC #general :Welcome to Aither IRC!");
					logger.Info($"Client connected: {client.Connected}");
					
					// Check the RSS as soon as the bot is started
					//OkToCheckRSS = true;

					while (client.Connected)
					{
						string? data;
						while ((data = reader.ReadLine()) != null)
						{
							string[] d = data.Split(' ');
							logger.Info($"Data: {data}");

							if (d[0] == "PING")
							{
								logger.Info("Recieved a PING from the server");
								SendMessageToServer(writer, $"PONG {d[1]}");
							}

							string channel;
							if (d.Length > 1)
							{
								switch (d[1])
								{
									case "JOIN":
										{
											string user = data.Split('!')[0][1..];
											channel = d[2][1..];

											logger.Info("USER JOIN detected");
											if (channel.ToLower() != announceChannel.ToLower() && user != botSettings.Nick)   // be quiet on the #announce channel and don't talk to yourself
											{
												SendMessageToServer(writer, @$"PRIVMSG {channel} :Welcome {user}");
											}
											break;
										}
									case "001": // Welcome
										{
											// join all channels 
											foreach (string chan in botSettings.ChannelsToJoin)
											{
												SendMessageToServer(writer, $"JOIN {chan}");
											}
											break;
										}
									case "376": // ":End of MOTD command"
										{
											// tell NickServ who we are
											SendMessageToServer(writer, $"PRIVMSG nickserv IDENTIFY {botSettings.NickServPassword}");
											break;
										}
								}
							}
							if (OkToCheckRSS)
							{
								List<Announce> announcements = new();
								//List<Announce>? announcments = ReadRSSFeed(freeleechRssFeed, announcements, true);
								List<Announce>? announcments = ReadRSSFeed(rssFeed, announcements, false);
								if (announcments.Any())
								{
									foreach (Announce item in announcments)
									{
										if (!HasTorrentBeenAnnounced(item.Name))
										{
											AnnounceTorrent(writer, item);

											// Also check the size of the torrentHistory.log file
											// If it's too large, we want to cut it down a bit
											ResizeHistoryLog(torrentHistoryLogFileName);
										}
									}
								}

								OkToCheckRSS = false;
							}
						}
					}
				}
				catch (Exception ex)
				{
					logger.Error($"Caught exception: {ex}");

					// shows the exception, sleeps for a little while and then tries to establish a new connection to the IRC server
					logger.Error(ex.ToString());
					Thread.Sleep(5000);
					_ = new IRCbot();
					Start();

					retry = ++retryCount <= botSettings.MaxRetries;
				}
			} while (retry);
		}

		/// <summary>
		/// Read the RSS feed
		/// </summary>
		/// <returns>List of announcements</returns>
		public static List<Announce>? ReadRSSFeed(string url, List<Announce> announcements, bool freeleech)
		{
			try
			{
				logger.Info($"Reading {url} RSS for new torrents");
				XmlReader reader = XmlReader.Create(url);
				SyndicationFeed feed = SyndicationFeed.Load(reader);
				reader.Close();
				foreach (SyndicationItem item in feed.Items)
				{
					string[] newItem = item.Summary.Text.Split("<strong>");

					Announce announce = new()
					{
						Name = $"Name: [{newItem[1].Trim()}]".Replace("Name</strong>: ","").Replace("<br>", ""),
						Category = $"Category: [{newItem[2].Trim()}]".Replace("Category</strong>: ", "").Replace("<br>", ""),
						Type = $"Type: [{newItem[3].Trim()}]".Replace("Type</strong>: ", "").Replace("<br>", ""),
						Resolution = $"Resolution: [{newItem[4].Trim()}]".Replace("Resolution</strong>: ", "").Replace("<br>", ""),
						Size = $"Size: [{newItem[5].Trim()}]".Replace("Size</strong>: ", "").Replace("<br>", ""),
						Uploader = $"Uploader: [{newItem[10].Split('\n')[1].Trim()}]".Replace("Anonymous Uploader", "Anonymous").Replace("Uploaded By ", ""),
						Url = $"Url: [{item.Links[0].Uri}]"
						//	FreeLeech = freeleech ? $"Freeleech: [Yes]" : "Freeleech: [No]"
					};
					announcements.Add(announce);
				}
				return announcements;
			}
			catch (Exception ex)
			{
				logger.Error($"Error while trying to read RSS Feed: {ex}");
				return null;
			}
		}

		/// <summary>
		/// Write name of torrent announced to logfile
		/// </summary>
		/// <param name="torrent"></param>
		public static void WriteTorrentNameToFile(string torrent)
		{
			using StreamWriter torrentWriter = File.AppendText($@"{AppDomain.CurrentDomain.BaseDirectory}{torrentHistoryLogFileName}");
			torrentWriter.WriteLine(torrent);
		}

		/// <summary>
		/// Write message to the announce channel of a new torrent
		/// </summary>
		/// <param name="writer"></param>
		/// <param name="torrent"></param>
		public static void AnnounceTorrent(StreamWriter writer, Announce torrent)
		{
			// post new torrent to #announce channel
			logger.Info($"Announcing {torrent.Name}");
			SendMessageToServer(writer, $"PRIVMSG {announceChannel} :{torrent.Category} {torrent.Type} {torrent.Name} {torrent.FreeLeech} {torrent.Size} {torrent.Uploader} {torrent.Url}");

			// write to the torrentHistory.log file
			WriteTorrentNameToFile(torrent.Name);

			// IRC Server thinks we are spamming if we write messages too quickly.
			Thread.Sleep(timeToSleepBetweenAnnouncements * 1000);
		}

		/// <summary>
		/// Determine if torrent has already been announced
		/// </summary>
		/// <param name="torrent"></param>
		/// <returns></returns>
		public static bool HasTorrentBeenAnnounced(string torrent)
		{
			string filePath = $@"{AppDomain.CurrentDomain.BaseDirectory}{torrentHistoryLogFileName}";
			List<string> torrents = File.ReadAllLines(filePath).TakeLast(500).ToList();
			foreach (string item in torrents)
			{
				if (item.ToLower().Contains(torrent.ToLower()))
				{
					return true;
				}
			}
			return false;
		}

		/// <summary>
		/// Send a message to the IRC server
		/// </summary>
		/// <param name="writer"></param>
		/// <param name="msg"></param>
		public static void SendMessageToServer(StreamWriter writer, string msg)
		{
			try
			{
				logger.Info(msg);
				writer.WriteLine($"{msg}\r\n");
				writer.Flush();
			}
			catch (Exception ex) 
			{
				logger.Error(ex);
			}
		}
		
		/// <summary>
		/// If left unchecked, the size of the history log of announced torrents grows too large
		/// This can negatively impact performance, so we'll make sure the file stays a manageable size
		/// </summary>
		/// <param name="filename"></param>
		public static void ResizeHistoryLog(string filename)
		{
			try
			{
				int lines = File.ReadAllLines($@"{AppDomain.CurrentDomain.BaseDirectory}{filename}").Length;
				if (lines > 100)
				{
					File.WriteAllLines($@"{AppDomain.CurrentDomain.BaseDirectory}{filename}", File.ReadAllLines($@"{AppDomain.CurrentDomain.BaseDirectory}{filename}").Skip(lines - 51).ToArray());
					logger.Info("Resized torrent history logfile");
				}
			}
			catch (Exception ex)
			{
				logger.Error(ex.Message);
			}
		}
	}
}