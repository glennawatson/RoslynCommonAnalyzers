// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>The SST2312 descriptor.</summary>
internal static partial class DesignRules
{
    /// <summary>SST2312 — a type is declared outside any namespace.</summary>
    public static readonly DiagnosticDescriptor TypeInGlobalNamespace = Create(
        "SST2312",
        "Types should be declared in a named namespace",
        "Move '{0}' into a named namespace",
        TypeInGlobalNamespaceDescription);

    /// <summary>The TypeInGlobalNamespace rule description.</summary>
    private const string TypeInGlobalNamespaceDescription =
        "A type in the global namespace is visible from every file in every project that references the assembly, with no way to opt out — a "
        + "consumer cannot 'using' their way around a name they did not ask for, and cannot avoid a collision with their own. A namespace is "
        + "the one tool the language gives for that, and it costs one line.";
}
