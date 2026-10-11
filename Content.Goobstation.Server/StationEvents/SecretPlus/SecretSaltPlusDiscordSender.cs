// SPDX-License-Identifier: AGPL-3.0-or-later

using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Content.Goobstation.Server.StationEvents.SecretPlus;

/// <summary>
/// Sends one private Discord webhook chunk without exposing the webhook token
/// in errors or log messages. The client is supplied so HTTP responses can be
/// tested without contacting Discord.
/// </summary>
public static class SecretSaltPlusDiscordSender
{
    /// <summary>
    /// Send a full private report in Discord-safe chunks. All callers must handle
    /// exceptions without logging the tokenized URL.
    /// </summary>
    public static async Task SendReportAsync(HttpClient client, string url, string report)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var destination)
            || destination.Scheme != Uri.UriSchemeHttps
            || (destination.Host != "discord.com" && destination.Host != "discordapp.com")
            || !destination.AbsolutePath.StartsWith("/api/webhooks/", StringComparison.Ordinal))
            throw new ArgumentException("Invalid Discord webhook destination.");

        var chunk = new StringBuilder();
        foreach (var rawLine in report.Split('\n'))
        {
            var line = rawLine.Length > 1700 ? rawLine[..1700] : rawLine;
            if (chunk.Length > 0 && chunk.Length + line.Length + 1 > 1900)
            {
                await SendChunkAsync(client, destination, chunk.ToString());
                chunk.Clear();
            }

            chunk.AppendLine(line);
        }

        if (chunk.Length > 0)
            await SendChunkAsync(client, destination, chunk.ToString());
    }

    public static async Task SendChunkAsync(HttpClient client, Uri destination, string message)
    {
        var payload = JsonSerializer.Serialize(new
        {
            content = message,
            allowed_mentions = new { parse = Array.Empty<string>() }
        });

        // Retry rate limits and transient Discord errors. Do not retry invalid
        // or revoked webhooks. The caller keeps tokens out of error logs.
        for (var attempt = 1; attempt <= 3; attempt++)
        {
            using var content = new StringContent(payload, Encoding.UTF8, "application/json");
            using var response = await client.PostAsync(destination, content);
            if (response.IsSuccessStatusCode)
                return;

            var status = (int) response.StatusCode;
            if (status != 429 && status < 500 || attempt == 3)
            {
                response.EnsureSuccessStatusCode();
                return;
            }

            var delay = response.Headers.RetryAfter?.Delta
                ?? TimeSpan.FromSeconds(attempt * 2);
            await Task.Delay(delay);
        }
    }
}
