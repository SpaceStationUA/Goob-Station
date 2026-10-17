// SPDX-License-Identifier: AGPL-3.0-or-later

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Content.Goobstation.Server.StationEvents.SecretPlus;

namespace Content.IntegrationTests.Tests.GameRules;

/// <summary>
/// No real Discord calls or webhook secrets: all HTTP responses are simulated.
/// </summary>
[TestFixture]
[Category("GameRuleTests")]
public sealed class SecretSaltDiscordSenderTest
{
    private static readonly Uri DummyWebhook = new("https://discord.com/api/webhooks/123/fake-token-not-used");

    [Test]
    public async Task TestRetriesRateLimitAndDisablesMentions()
    {
        using var fake = new FakeDiscordHandler(HttpStatusCode.TooManyRequests, HttpStatusCode.NoContent);
        using var client = new HttpClient(fake);

        await SecretSaltPlusDiscordSender.SendChunkAsync(client, DummyWebhook, "Привіт @everyone");

        Assert.That(fake.CallCount, Is.EqualTo(2));
        Assert.That(fake.Payloads[0], Is.EqualTo(fake.Payloads[1]));

        using var payload = JsonDocument.Parse(fake.Payloads[0]);
        Assert.That(payload.RootElement.GetProperty("content").GetString(), Is.EqualTo("Привіт @everyone"));
        Assert.That(payload.RootElement.GetProperty("allowed_mentions").GetProperty("parse").GetArrayLength(), Is.Zero);
    }

    [Test]
    public void TestRevokedWebhookDoesNotRetry()
    {
        using var fake = new FakeDiscordHandler(HttpStatusCode.NotFound);
        using var client = new HttpClient(fake);

        Assert.ThrowsAsync<HttpRequestException>(async () =>
            await SecretSaltPlusDiscordSender.SendChunkAsync(client, DummyWebhook, "Test"));

        Assert.That(fake.CallCount, Is.EqualTo(1));
    }

    [Test]
    public void TestStopsAfterThreeRateLimitFailures()
    {
        using var fake = new FakeDiscordHandler(
            HttpStatusCode.TooManyRequests,
            HttpStatusCode.TooManyRequests,
            HttpStatusCode.TooManyRequests);
        using var client = new HttpClient(fake);

        Assert.ThrowsAsync<HttpRequestException>(async () =>
            await SecretSaltPlusDiscordSender.SendChunkAsync(client, DummyWebhook, "Test"));

        Assert.That(fake.CallCount, Is.EqualTo(3));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void TestLargeRetryAfterCancelsWithoutPrematureRetry(bool absoluteDate)
    {
        using var cancellation = new CancellationTokenSource();
        using var fake = new LongRetryAfterHandler(cancellation, absoluteDate);
        using var client = new HttpClient(fake);

        // If Retry-After is an hour, a shorter report-wide budget must
        // abort delivery rather than violate Discord's rate limit.
        Assert.ThrowsAsync<TaskCanceledException>(async () =>
            await SecretSaltPlusDiscordSender.SendChunkAsync(
                client, DummyWebhook, "Report", cancellation.Token));

        Assert.That(fake.CallCount, Is.EqualTo(1),
            "The sender must not retry while Discord's Retry-After is active.");
    }

    [Test]
    public async Task TestTransportFailureRetriesOnlyUnsentChunk()
    {
        // First chunk succeeds. The second chunk fails once at the network
        // layer and then succeeds; an earlier delivered chunk is not repeated.
        using var fake = new FaultingDiscordHandler(null,
            new HttpRequestException("Simulated connection reset"), null);
        using var client = new HttpClient(fake);
        var report = new string('A', 1900) + new string('B', 50);

        await SecretSaltPlusDiscordSender.SendReportAsync(client, DummyWebhook.ToString(), report);

        Assert.That(fake.CallCount, Is.EqualTo(3));
        Assert.That(fake.Payloads[0], Is.Not.EqualTo(fake.Payloads[1]));
        Assert.That(fake.Payloads[1], Is.EqualTo(fake.Payloads[2]));
    }

    [Test]
    public async Task TestRequestTimeoutIsRetried()
    {
        using var fake = new FaultingDiscordHandler(
            new TaskCanceledException("Simulated request timeout"), null);
        using var client = new HttpClient(fake);

        await SecretSaltPlusDiscordSender.SendChunkAsync(client, DummyWebhook, "Retry timeout");

        Assert.That(fake.CallCount, Is.EqualTo(2));
        Assert.That(fake.Payloads[0], Is.EqualTo(fake.Payloads[1]));
    }

    [Test]
    public void TestStopsAfterThreeTransportFailures()
    {
        using var fake = new FaultingDiscordHandler(
            new HttpRequestException("Connection failure 1"),
            new HttpRequestException("Connection failure 2"),
            new HttpRequestException("Connection failure 3"));
        using var client = new HttpClient(fake);

        Assert.ThrowsAsync<HttpRequestException>(async () =>
            await SecretSaltPlusDiscordSender.SendChunkAsync(client, DummyWebhook, "Test"));

        Assert.That(fake.CallCount, Is.EqualTo(3));
        Assert.That(fake.Payloads[0], Is.EqualTo(fake.Payloads[1]));
        Assert.That(fake.Payloads[1], Is.EqualTo(fake.Payloads[2]));
    }

    [Test]
    public async Task TestLongReportSplitsIntoDiscordSafeChunks()
    {
        using var fake = new FakeDiscordHandler(
            HttpStatusCode.NoContent, HttpStatusCode.NoContent);
        using var client = new HttpClient(fake);
        var report = string.Join("\n", new[] { new string('A', 1200), new string('B', 1200) });

        await SecretSaltPlusDiscordSender.SendReportAsync(client, DummyWebhook.ToString(), report);

        Assert.That(fake.CallCount, Is.EqualTo(2));
        foreach (var json in fake.Payloads)
        {
            using var payload = JsonDocument.Parse(json);
            Assert.That(payload.RootElement.GetProperty("content").GetString()!.Length,
                Is.LessThanOrEqualTo(1900));
            Assert.That(payload.RootElement.GetProperty("allowed_mentions")
                .GetProperty("parse").GetArrayLength(), Is.Zero);
        }
    }

    [Test]
    public async Task TestLongUnbrokenReportPreservesEveryCharacterAndEmoji()
    {
        // Place a two-code-unit emoji immediately across the nominal chunk
        // boundary. A naive substring split may corrupt the surrogate pair.
        using var fake = new FakeDiscordHandler(
            HttpStatusCode.NoContent, HttpStatusCode.NoContent, HttpStatusCode.NoContent);
        using var client = new HttpClient(fake);
        var report = new string('А', 1899) + "🧂\n" + new string('Ж', 2600)
            + "\nКінець звіту @everyone";

        await SecretSaltPlusDiscordSender.SendReportAsync(client, DummyWebhook.ToString(), report);

        Assert.That(fake.CallCount, Is.EqualTo(3));
        var received = new StringBuilder();
        foreach (var json in fake.Payloads)
        {
            using var payload = JsonDocument.Parse(json);
            var part = payload.RootElement.GetProperty("content").GetString()!;
            Assert.That(part.Length, Is.GreaterThan(0));
            Assert.That(part.Length, Is.LessThanOrEqualTo(1900));
            Assert.That(payload.RootElement.GetProperty("allowed_mentions")
                .GetProperty("parse").GetArrayLength(), Is.Zero);
            received.Append(part);
        }

        Assert.That(received.ToString(), Is.EqualTo(report),
            "The author must receive the complete report, not silently truncated lines.");
    }

    [Test]
    public void TestPrivateReportRejectsUnexpectedDestination()
    {
        using var fake = new FakeDiscordHandler(HttpStatusCode.NoContent);
        using var client = new HttpClient(fake);

        Assert.ThrowsAsync<ArgumentException>(async () =>
            await SecretSaltPlusDiscordSender.SendReportAsync(client,
                "https://evil.example/api/webhooks/123/fake-token", "Private report"));

        Assert.That(fake.CallCount, Is.Zero);
    }

    private sealed class LongRetryAfterHandler : HttpMessageHandler
    {
        private readonly CancellationTokenSource _cancellation;
        private readonly bool _absoluteDate;
        public int CallCount;

        public LongRetryAfterHandler(CancellationTokenSource cancellation, bool absoluteDate)
        {
            _cancellation = cancellation;
            _absoluteDate = absoluteDate;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            CallCount++;
            var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
            response.Headers.RetryAfter = _absoluteDate
                ? new RetryConditionHeaderValue(DateTimeOffset.UtcNow.AddHours(1))
                : new RetryConditionHeaderValue(TimeSpan.FromHours(1));

            // Trigger cancellation after the response has been returned and
            // the long Retry-After delay is about to run.
            _cancellation.CancelAfter(TimeSpan.FromMilliseconds(25));
            return Task.FromResult(response);
        }
    }

    private sealed class FaultingDiscordHandler : HttpMessageHandler
    {
        private readonly Queue<Exception?> _failures;
        public readonly List<string> Payloads = new();
        public int CallCount;

        public FaultingDiscordHandler(params Exception?[] failures)
        {
            _failures = new Queue<Exception?>(failures);
        }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            CallCount++;
            Payloads.Add(await request.Content!.ReadAsStringAsync(cancellationToken));
            var failure = _failures.Dequeue();
            if (failure != null)
                throw failure;

            return new HttpResponseMessage(HttpStatusCode.NoContent);
        }
    }

    private sealed class FakeDiscordHandler : HttpMessageHandler
    {
        private readonly Queue<HttpStatusCode> _responses;
        public readonly List<string> Payloads = new();
        public int CallCount;

        public FakeDiscordHandler(params HttpStatusCode[] responses)
        {
            _responses = new Queue<HttpStatusCode>(responses);
        }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            CallCount++;
            Payloads.Add(await request.Content!.ReadAsStringAsync(cancellationToken));

            var response = new HttpResponseMessage(_responses.Dequeue());
            if (response.StatusCode == HttpStatusCode.TooManyRequests)
                response.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.Zero);
            return response;
        }
    }
}
