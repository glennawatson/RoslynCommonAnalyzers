// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>The SST2314 descriptor.</summary>
internal static partial class DesignRules
{
    /// <summary>SST2314 — an obsolete member explains itself but cannot be suppressed on its own.</summary>
    public static readonly DiagnosticDescriptor ObsoleteWithoutDiagnosticId = Create(
        "SST2314",
        "Obsolete attributes should carry a DiagnosticId",
        "The [Obsolete] on '{0}' has a message but no DiagnosticId, so every caller gets the same CS0618",
        ObsoleteWithoutDiagnosticIdDescription);

    /// <summary>The ObsoleteWithoutDiagnosticId rule description.</summary>
    private const string ObsoleteWithoutDiagnosticIdDescription =
        "A message tells a caller what to do; a 'DiagnosticId' lets them do it. Without one, every deprecation in every library collapses "
        + "into the same CS0618, so a caller cannot suppress one migration they have already scheduled without suppressing all of them, and "
        + "cannot make one of them an error while the rest stay warnings. Give the attribute an id of your own and a 'UrlFormat', and the "
        + "warning arrives with a name the caller can act on and a link to the instructions.";
}
