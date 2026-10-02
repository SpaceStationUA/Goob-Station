// SPDX-FileCopyrightText: 2026 Pirate Development Team
// SPDX-License-Identifier: AGPL-3.0-or-later

using System;
using System.IO;
using Robust.UnitTesting.Pool;

namespace Content.YAMLLinter;

/// <summary>
///     A non-NUnit <see cref="ITestContextLike"/> for the linter's pool usage.
///
///     The linter is a plain console program: it borrows a server/client pair
///     from the integration-test pool without an NUnit run behind it. The pool
///     defaults to wrapping NUnit's ambient <c>TestContext</c> when no context
///     is supplied, and on that path it creates a per-pair "gravestone" file
///     under <c>TestContext.CurrentContext.WorkDirectory</c> -- which throws
///     outside a test run, because NUnit's assembly builder never ran.
///     Supplying our own context keeps the pool off that path.
/// </summary>
internal sealed class ConsoleTestContext : ITestContextLike
{
    public ConsoleTestContext(string name, TextWriter output)
    {
        FullName = name;
        Out = output;
    }

    public string FullName { get; }

    public TextWriter Out { get; }
}
