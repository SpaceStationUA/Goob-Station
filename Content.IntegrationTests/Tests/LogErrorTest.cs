// SPDX-License-Identifier: MIT

using Robust.Shared.Configuration;
using Robust.Shared.Log;
using Robust.UnitTesting;

namespace Content.IntegrationTests.Tests;

public sealed class LogErrorTest
{
    /// <summary>
    ///     This test ensures that error logs cause tests to fail.
    /// </summary>
    [Test]
    public async Task TestLogErrorCausesTestFailure()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var server = pair.Server;
        var client = pair.Client;

        var cfg = server.ResolveDependency<IConfigurationManager>();
        var logmill = server.ResolveDependency<ILogManager>().RootSawmill;

        // Default cvar is properly configured
        Assert.That(cfg.GetCVar(RTCVars.FailureLogLevel), Is.EqualTo(LogLevel.Error));

        // Warnings don't cause tests to fail.
        await server.WaitPost(() => logmill.Warning("test"));
        Assert.That(pair.ServerLogHandler.FailingLogs, Is.Empty,
            "A warning was recorded as a test failure.");

        // But errors do. The harness records a failing log on the pair's log
        // handler and reports it when the pair is disposed; it does not throw
        // from the log call itself.
        await server.WaitPost(() => logmill.Error("test"));
        Assert.That(pair.ServerLogHandler.FailingLogs, Has.Count.EqualTo(1),
            "An error log was not recorded as a test failure.");
        Assert.That(pair.ServerLogHandler.FailingLogs[0], Does.Contain("[ERRO] root: test"),
            $"Unexpected failure record: {pair.ServerLogHandler.FailingLogs[0]}");

        // Warnings after that must still not be recorded.
        await server.WaitPost(() => logmill.Warning("test2"));
        Assert.That(pair.ServerLogHandler.FailingLogs, Has.Count.EqualTo(1),
            "A warning was recorded as a test failure after an error.");

        // This test logs an error on purpose. Drop the record so the pair goes
        // back to the pool clean -- otherwise the deliberate error fails us at
        // dispose, which is exactly the behaviour under test. ClearContext also
        // drops the writer, so restore it before handing the pair over.
        pair.ServerLogHandler.ClearContext();
        pair.ServerLogHandler.ActivateContext(TestContext.Out);

        await pair.CleanReturnAsync();
    }
}