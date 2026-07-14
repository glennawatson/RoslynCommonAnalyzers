// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>The SST2308 descriptor.</summary>
internal static partial class DesignRules
{
    /// <summary>SST2308 — an obsolete member does not say what to use instead.</summary>
    public static readonly DiagnosticDescriptor ObsoleteWithoutExplanation = Create(
        "SST2308",
        "Obsolete attributes should explain what to use instead",
        "The [Obsolete] on '{0}' has no message; say why and what replaces it",
        ObsoleteWithoutExplanationDescription);

    /// <summary>The ObsoleteWithoutExplanation rule description.</summary>
    private const string ObsoleteWithoutExplanationDescription =
        "'[Obsolete]' with no message tells a caller their code is wrong and nothing else. The message is the whole value of the "
        + "attribute — it is the one place the author can hand the reader the migration, at exactly the moment they need it.";
}
