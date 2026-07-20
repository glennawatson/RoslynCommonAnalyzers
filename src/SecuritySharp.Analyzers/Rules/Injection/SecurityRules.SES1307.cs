// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace SecuritySharp.Analyzers;

/// <summary>The SES1307 descriptor.</summary>
internal static partial class SecurityRules
{
    /// <summary>SES1307 — an insecure, predictable temporary file is created with <c>Path.GetTempFileName()</c>.</summary>
    public static readonly DiagnosticDescriptor InsecureTempFile = Create(
        "SES1307",
        "Do not create predictable temporary files with Path.GetTempFileName",
        "'Path.GetTempFileName()' creates a predictable, world-readable temporary file that is open to a time-of-check/time-of-use race and fails after 65535 undeleted files; replace it with {0}",
        Injection,
        InsecureTempFileDescription);

    /// <summary>The SES1307 rule description.</summary>
    private const string InsecureTempFileDescription =
        "'Path.GetTempFileName()' creates a zero-byte file with a guessable name (tmpXXXX.tmp) in the shared, world-readable "
        + "temp directory. Because the name is predictable and the directory is writable by every local user, an attacker can "
        + "pre-create or swap the file between the moment it is made and the moment it is opened -- a time-of-check/time-of-use "
        + "race that leads to information disclosure, tampering, or a symlink attack. The API also draws from only 65535 names "
        + "and throws once that many undeleted files exist, turning a leak into a denial of service (CWE-377). Obtain an "
        + "unpredictable name with 'Path.GetRandomFileName()', or create an isolated per-run directory with "
        + "'Directory.CreateTempSubdirectory()' on .NET 7 and later, and open the file with a mode that fails if it already "
        + "exists.";
}
