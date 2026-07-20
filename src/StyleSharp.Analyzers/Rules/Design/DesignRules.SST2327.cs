// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>The SST2327 descriptor.</summary>
internal static partial class DesignRules
{
    /// <summary>SST2327 — a type inspects its own runtime type against a specific named type.</summary>
    public static readonly DiagnosticDescriptor SelfTypeCheck = Create(
        "SST2327",
        "Do not branch on a type's own runtime type",
        "This type tests its own runtime type against '{0}'; move the type-specific behaviour into a virtual or overridden member",
        SelfTypeCheckDescription);

    /// <summary>The SelfTypeCheck rule description.</summary>
    private const string SelfTypeCheckDescription =
        "A type that checks its own runtime type — 'this is Derived', 'this as Derived', or "
        + "'this.GetType() == typeof(Derived)' — branches on which of its own subtypes it happens to be. That "
        + "defeats polymorphism: the base takes on knowledge of its derivations, and every new subtype forces the "
        + "branch to be revisited, so a case is easily missed. Put the behaviour that varies by type in a virtual "
        + "member the base declares and each derived type overrides, so the runtime dispatches to the right "
        + "implementation on its own. A capability check against an interface, and a test of some other value "
        + "rather than 'this', are left alone.";
}
