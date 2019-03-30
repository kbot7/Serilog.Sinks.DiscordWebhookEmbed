using Discord;
using Discord.Rest;
using Discord.Webhook;
using Serilog.Core;
using Serilog.Events;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace Serilog.Sinks.DiscordWebhookEmbed
{
	public class DiscordWebhookSerilogSink : ILogEventSink
	{
		private readonly IFormatProvider _formatProvider;
		private readonly DiscordWebhookClient _client;
		private readonly ConcurrentQueue<EmbedFieldBuilder> _messageQueue;
		private readonly Timer _timer;

		public DiscordWebhookSerilogSink(ulong webhookId, string webhookSecret, int sendIntervalMs = 2500, DiscordRestConfig restConfig = null, IFormatProvider formatProvider = null)
		{
			_formatProvider = formatProvider;
			_client = new DiscordWebhookClient(webhookId, webhookSecret,
				restConfig ?? new DiscordRestConfig());
			_messageQueue = new ConcurrentQueue<EmbedFieldBuilder>();
			_timer = new Timer(TimerCallback, null, sendIntervalMs, sendIntervalMs);
		}


		public void Emit(LogEvent logEvent)
		{
			logEvent.Properties.TryGetValue("SourceContext", out var sourceContext);
			var sourceString = sourceContext.ToString().Trim(' ', '"');

			var fullMessage = logEvent.RenderMessage(_formatProvider);

			var messages = new List<string>();
			messages.SplitMessageOnLineOrLimit(fullMessage, 0);

			for (int i = 0; i < messages.Count; i++)
			{
				var embedFieldBuilder = new EmbedFieldBuilder
				{
					Name = $"{logEvent.Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{logEvent.Level}] {(i > 0 ? "(Continued)" : "")} \n{sourceString}",
					Value = messages[i],
					IsInline = false
				};
				_messageQueue.Enqueue(embedFieldBuilder);
			}
		}

		private void TimerCallback(object state)
		{
			try
			{
				// Take 25 messages or until the queue is empty
				int taken = 0;
				var fields = new List<EmbedFieldBuilder>();
				while (_messageQueue.TryDequeue(out var fieldBuilder) && taken < 25)
				{
					fields.Add(fieldBuilder);
					taken++;
				}

				// Send message if there were any retrieved
				if (fields.Any())
				{
					var embedBuilder = new EmbedBuilder();
					foreach (var field in fields)
					{
						embedBuilder.AddField(field);
					}
					_client.SendMessageAsync(embeds: new[] { embedBuilder.Build() });
				}
			}
			catch (Exception ex)
			{
				Console.WriteLine(ex);
			}
		}
	}
	internal static class StringUtilities
	{
		public static void SplitMessageOnLineOrLimit(this List<string> list, string fullMessage, int startIndex, int maxMessageSize = 1024)
		{
			// If the full message is less than 1024, add it to list and return. No need to split
			if (fullMessage.Length < 1024)
			{
				list.Add(fullMessage);
				return;
			}

			// If the start index and message size are greater than the full message,
			// Then take a substring to the end of the full message and return
			if (startIndex + maxMessageSize > fullMessage.Length)
			{
				list.Add(fullMessage.Substring(startIndex, fullMessage.Length - startIndex));
				return;
			}

			var first1024 = fullMessage.Substring(startIndex, maxMessageSize);
			var lastLineBreakIndex = first1024.LastIndexOf('\n');
			int takeCount = lastLineBreakIndex > -1 ?
				lastLineBreakIndex + 1 :
				maxMessageSize;

			list.Add(fullMessage.Substring(startIndex, takeCount));
			SplitMessageOnLineOrLimit(list, fullMessage, lastLineBreakIndex + startIndex);
		}
	}
}
