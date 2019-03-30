using Discord.Rest;
using Serilog.Configuration;
using System;

namespace Serilog.Sinks.DiscordWebhookEmbed
{
	public static class SerilogDiscordExtensions
	{
		public static LoggerConfiguration DiscordWebhook(
			this LoggerSinkConfiguration loggerConfiguration,
			ulong webhookId,
			string webhookSecret,
			int sendIntervalMs = 2500,
			DiscordRestConfig restConfig = null,
			IFormatProvider formatProvider = null)
		{
			return loggerConfiguration.Sink(new DiscordWebhookSerilogSink(webhookId, webhookSecret, sendIntervalMs, restConfig,
				formatProvider));
		}
	}
}
