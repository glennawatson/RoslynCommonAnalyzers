// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>The SST2321 descriptor.</summary>
internal static partial class DesignRules
{
    /// <summary>SST2321 — library code tears down the whole host process instead of throwing.</summary>
    public static readonly DiagnosticDescriptor LibraryProcessTermination = Create(
        "SST2321",
        "Do not terminate the process from library code",
        "'{0}' terminates the entire host process; a library should throw and let its host decide how to shut down{1}",
        LibraryProcessTerminationDescription);

    /// <summary>The LibraryProcessTermination rule description.</summary>
    private const string LibraryProcessTerminationDescription =
        "A class library does not own the process it runs in. The same code may be loaded into a web server serving other "
        + "requests, a test host running other tests, or a long-lived tool. Tearing the whole process down takes all of that "
        + "with it, on a decision the caller never got to make. A library that hits an unrecoverable condition should throw and "
        + "let its host decide how to shut down. The blunter of the two calls skips every pending 'finally' block and writes a "
        + "crash dump before it goes, so nothing gets a chance to flush, roll back, or clean up.";
}
