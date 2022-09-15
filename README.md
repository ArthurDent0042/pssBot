ircBot for announcing new uploads from website to irc Server
bot is to be run on irc server and is located at /home/stcbot/stcBot

To run:
/home/stcbot/stcBot/stcBot > /dev/null &

and then logout of the ssh session

To kill the bot:

ps -aux | grep stcBot
and then issue the command to kill it, using the PID of the job
kill -9 <pid>
  
Announced torrents are located in a logfile: /home/stcbot/stcBot/torrentHistory.log
  Occasionally, you'll want to prune rows from the top of this file, as it can grow very large and may impact performance.
  
Logs of the actions taken by the bot are located in
  /home/stcbot/stcBot/Logs

This bot makes use of 2 RSS Feeds.  One feed lists everything EXCEPT freeleech, and the other lists ONLY freeleech. 
  This was implemented this way because it was not possible to include a freeleech indicator in the RSS feed.
  
  
