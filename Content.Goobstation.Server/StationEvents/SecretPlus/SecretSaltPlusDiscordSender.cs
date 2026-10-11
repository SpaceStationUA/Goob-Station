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
