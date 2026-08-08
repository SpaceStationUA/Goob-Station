// SPDX-License-Identifier: MPL-2.0

using Robust.Client;

namespace Content.Pirate.Client;

internal sealed class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        ContentStart.Start(args);
    }
}
