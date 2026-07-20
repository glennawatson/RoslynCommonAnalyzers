// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace SecuritySharp.Analyzers;

/// <summary>The SES1308 descriptor.</summary>
internal static partial class SecurityRules
{
    /// <summary>SES1308 — a file or directory is created group- or world-writable via a Unix file mode.</summary>
    public static readonly DiagnosticDescriptor OverPermissiveUnixFileMode = Create(
        "SES1308",
        "Do not create a group- or world-writable file or directory",
        "The Unix file mode set through '{0}' includes a group or other write bit; a group- or world-writable file or directory lets other local users modify its contents",
        Injection,
        OverPermissiveUnixFileModeDescription);

    /// <summary>The SES1308 rule description.</summary>
    private const string OverPermissiveUnixFileModeDescription =
        "A Unix file mode that carries the group-write (0o020) or other-write (0o002) bit lets every local user in the file's "
        + "group -- or, for other-write, every local user on the machine -- change the file's contents. Whatever the process "
        + "later reads back from that path (a config file, a script it executes, a data file it trusts) can have been replaced "
        + "or tampered with by another account, so an over-permissive mode is a local privilege-escalation and integrity hole "
        + "(CWE-732). The rule reports a constant mode -- a single member such as 'UnixFileMode.OtherWrite', an OR-combination, "
        + "or a broad 0o777-style combo -- passed to a filesystem permission API ('File.SetUnixFileMode', "
        + "'Directory.CreateDirectory', 'FileInfo.UnixFileMode', 'DirectoryInfo.UnixFileMode', or "
        + "'FileStreamOptions.UnixCreateMode') whenever the group-write or other-write bit is set. Grant write only to the "
        + "owner: drop 'GroupWrite' and 'OtherWrite' and keep the mode as tight as the file's real sharing needs.";
}
