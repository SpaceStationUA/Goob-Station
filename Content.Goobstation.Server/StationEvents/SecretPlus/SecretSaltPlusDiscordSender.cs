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

        const int maxContentLength = 1900;
        var chunk = new StringBuilder(maxContentLength);

        // Split the entire report without deleting long lines, comments or
        // newlines. The previous implementation silently discarded everything
        // after character 1700 on each line. Keep UTF-16 surrogate pairs
        // together so that emoji and other non-BMP text remain valid.
        for (var i = 0; i < report.Length; i++)
        {
            var scalarLength = i + 1 < report.Length
                && char.IsHighSurrogate(report[i])
                && char.IsLowSurrogate(report[i + 1])
                ? 2 : 1;

            if (chunk.Length + scalarLength > maxContentLength)
            {
                await SendChunkAsync(client, destination, chunk.ToString());
                chunk.Clear();
            }

            chunk.Append(report, i, scalarLength);
            i += scalarLength - 1;
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
            HttpResponseMessage response;
            try
            {
                // Catch only transport failures here. Non-retryable HTTP
                // responses are checked below and must not be retried.
                response = await client.PostAsync(destination, content);
            }
            catch (HttpRequestException) when (attempt < 3)
            {
                await Task.Delay(TimeSpan.FromSeconds(attempt * 2));
                continue;
            }
            catch (TaskCanceledException) when (attempt < 3)
            {
                // HttpClient reports request timeouts as TaskCanceledException.
                // Retrying just this chunk avoids repeating earlier chunks.
                await Task.Delay(TimeSpan.FromSeconds(attempt * 2));
                continue;
            }

            using (response)
            {
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
}
