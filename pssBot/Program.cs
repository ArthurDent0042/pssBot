using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using NLog;
using NLog.Extensions.Logging;
using pssBot.Models;
using System.Net.Sockets;

namespace pssBot
{
	public class IRCbot
	{
		private static string apiKey = string.Empty;
		private static string announceChannel = string.Empty;
		private static BotSettings? botSettings;
		private static string torrentHistoryLogFileName = string.Empty;
		private static string previousApiResult = string.Empty;
		private static List<Announce> announcements = new();
        private static readonly IConfigurationRoot config = new ConfigurationBuilder()
						.SetBasePath(Directory.GetCurrentDirectory())
						.AddJsonFile(path: "appSettings.json", optional: false, reloadOnChange: true)
						.Build();
        public static readonly Logger logger = LogManager.Setup().LoadConfigurationFromSection(config).GetCurrentClassLogger();
        private static readonly HttpClient client = new()
		{
			BaseAddress = new Uri(config.GetValue<string>("APIBaseAddress"))
		};

		static async Task Main()
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
				APICheckFrequency = config.GetValue<int>("ircBotConfig:APICheckFrequencyInSeconds")
			};
			botSettings.User = $"USER {botSettings.Nick} 0 * :{botSettings.Nick}";

			// API Key
			apiKey = config.GetValue<string>("APIKey");

			// Announce channel
			announceChannel = config.GetValue<string>("announceChannel");

            // name of torrent history logfile
            torrentHistoryLogFileName = config.GetValue<string>("torrentHistoryLogFilename");

			// verify history logfile exists
			if (!File.Exists($@"{AppDomain.CurrentDomain.BaseDirectory}{torrentHistoryLogFileName}"))
			{
				File.Create($@"{AppDomain.CurrentDomain.BaseDirectory}{torrentHistoryLogFileName}").Close();
			}
			

			// Let's fire up the bot
			await Start();
		}

		/// <summary>
		/// Used to fire off actions we want to perform at specific intervals
		/// </summary>
		public static void SiteTimer(StreamWriter writer)
		{
			System.Timers.Timer timer = new(interval: botSettings.APICheckFrequency * 1000)
			{
				AutoReset = true
			};
			timer.Elapsed += (source, e) => HandleTimerElapsed(writer);
			timer.Start();
		}

		public static async Task HandleTimerElapsed(StreamWriter writer)
		{
			ReadAPI().Wait();
			if (announcements.Any())
			{
				foreach (Announce item in announcements)
				{
					await AnnounceTorrent(writer, item);

					// Also check the size of the torrentHistory.log file
					// If it's too large, we want to cut it down a bit
					ResizeHistoryLog(torrentHistoryLogFileName);
				}
				announcements.Clear();
			}
		}

		public static async Task Start()
		{
			logger.Info($"{botSettings.Nick} announceBot app starting");
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
					logger.Info($"Client connected: {client.Connected}");

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
									case "JOIN": // User has joined a channel
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
									case "376": // End of MOTD command
										{
											// tell NickServ who we are
											SendMessageToServer(writer, $"PRIVMSG nickserv IDENTIFY {botSettings.NickServPassword}");

											// we've successfully connected to irc - now start our timer
											SiteTimer(writer);
											break;
										}
								}
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
					await Start();

					retry = ++retryCount <= botSettings.MaxRetries;
				}
			} while (retry);
		}

		public static async Task<List<Announce>?> ReadAPI()
		{
			try
			{
				HttpResponseMessage response = await client.GetAsync($"api/torrents?api_token={apiKey}");
				if (response.IsSuccessStatusCode)
				{
					string result = response.Content.ReadAsStringAsync().Result;
					// check to see if our result is the same length as the previous result
					// if the same, then no need to parse the data looking for new torrents
					if (result != previousApiResult)
					{
						previousApiResult = result;
						Torrent? torrents = JsonConvert.DeserializeObject<Torrent>(result);
						if (torrents != null)
						{
							foreach (Datum data in torrents.Data)
							{
								if (!HasTorrentBeenAnnounced(data.Id))
								{
									Announce announce = new()
									{
										Id = data.Id,
										Category = data.Attributes.Category,
										Name = data.Attributes.Name,
										Size = FormatBytes(Convert.ToInt64(data.Attributes.Size)),
										Type = data.Attributes.Type,
										Resolution = data.Attributes.Resolution ?? null,
										Uploader = data.Attributes.Uploader,
										Url = data.Attributes.Download_link[..^33].Replace("torrent", "torrents"),
										FreeLeech = data.Attributes.Freeleech,
										DoubleUpload = data.Attributes.Double_upload.ToString() == "1" ? "Yes" : "No"
									};
									announcements.Add(announce);

                                    // write to the torrentHistory.log file
                                    WriteTorrentIdToFile(data.Id);
                                }
                            }
						}
					}
				}
				return announcements;
			}
			catch (Exception ex)
			{
				logger.Error(ex.Message);
				return null;
			}
		}

		/// <summary>
		/// Write Id of torrent announced to logfile
		/// </summary>
		/// <param name="torrent"></param>
		public static void WriteTorrentIdToFile(string torrent)
		{
			try
			{
				using StreamWriter torrentWriter = File.AppendText($@"{AppDomain.CurrentDomain.BaseDirectory}{torrentHistoryLogFileName}");
				torrentWriter.WriteLine(torrent);
			}
			catch (Exception ex)
			{
				logger.Error(ex.Message);
			}
		}

		/// <summary>
		/// Write message to the announce channel of a new torrent
		/// </summary>
		/// <param name="writer"></param>
		/// <param name="torrent"></param>
		public async static Task AnnounceTorrent(StreamWriter writer, Announce torrent)
		{
			try
			{
				// post new torrent to #announce channel
				logger.Info($"Announcing {torrent.Name}");
				SendMessageToServer(writer, $"PRIVMSG {announceChannel} :Category [{torrent.Category}] Type [{torrent.Type}] Name [{torrent.Name}] Resolution [{torrent.Resolution}] Freeleech [{torrent.FreeLeech}] Double Upload [{torrent.DoubleUpload}] Size [{torrent.Size}] Uploader [{torrent.Uploader}] Url [{torrent.Url}]");
			}
			catch (Exception ex)
			{
				logger.Error(ex.Message);
			}
		}

		/// <summary>
		/// Determine if torrent has already been announced
		/// </summary>
		/// <param name="torrent"></param>
		/// <returns></returns>
		public static bool HasTorrentBeenAnnounced(string id)
		{
			try
			{
				string filePath = $@"{AppDomain.CurrentDomain.BaseDirectory}{torrentHistoryLogFileName}";
				List<string> torrents = File.ReadAllLines(filePath).TakeLast(500).ToList();
				foreach (string item in torrents)
				{
					if (item.ToLower().Contains(id.ToLower()))
					{
						return true;
					}
				}
				return false;
			}
			catch (Exception ex)
			{
				logger.Error(ex.Message);
				return true;
			}
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

		private static string FormatBytes(Int64 bytes)
		{
			try
			{
				string[] Suffix = { "B", "kB", "MB", "GB", "TB" };
				int i;
				double dblSByte = bytes;
				for (i = 0; i < Suffix.Length && bytes >= 1024; i++, bytes /= 1024)
				{
					dblSByte = bytes / 1024.0;
				}

				return String.Format("{0:0.##} {1}", dblSByte, Suffix[i]);
			}
			catch (Exception ex)
			{
				logger.Error(ex.Message);
				return string.Empty;
			}
		}
	}
}