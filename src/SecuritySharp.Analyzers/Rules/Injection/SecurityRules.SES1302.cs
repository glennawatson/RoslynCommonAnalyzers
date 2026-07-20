// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace SecuritySharp.Analyzers;

/// <summary>The SES1302 descriptor.</summary>
internal static partial class SecurityRules
{
    /// <summary>SES1302 — a shell-executed process is launched with a non-constant filename.</summary>
    public static readonly DiagnosticDescriptor ShellExecuteFileName = Create(
        "SES1302",
        "A shell-executed process must not use a non-constant FileName",
        "'UseShellExecute' is true while 'FileName' is a non-constant value; the OS shell resolves and parses the filename, so a data-derived value is a command-injection and unexpected-program risk",
        Injection,
        ShellExecuteFileNameDescription);

    /// <summary>The SES1302 rule description.</summary>
    private const string ShellExecuteFileNameDescription =
        "When 'ProcessStartInfo.UseShellExecute' is true the operating-system shell -- not the process loader -- resolves and "
        + "parses 'FileName': it applies PATH lookup, expands the value, and on Windows can select a registered handler or run a "
        + "document/URL. Feeding that a value the program did not fix at compile time lets an attacker steer which program runs "
        + "or inject extra shell behaviour, so a non-constant filename under shell execution is a command-injection and "
        + "unexpected-program risk. This rule reports only the precise local shape -- a single 'new ProcessStartInfo' object "
        + "creation whose initializer sets 'UseShellExecute = true' and whose 'FileName' (from the initializer or the "
        + "constructor argument) is not a compile-time constant. Set 'UseShellExecute = false' so the loader launches the "
        + "executable directly, or restrict 'FileName' to a fixed, validated program path and pass user data through "
        + "'ArgumentList' instead.";
}
