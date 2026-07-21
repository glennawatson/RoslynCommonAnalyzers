// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>The SST2332 descriptor.</summary>
internal static partial class DesignRules
{
    /// <summary>SST2332 — a private setter is only written during construction and can be a get-only property.</summary>
    public static readonly DiagnosticDescriptor PrivateSetterOnlyWrittenDuringConstruction = Create(
        "SST2332",
        "Make a construction-only property get-only",
        "'{0}' has a private setter that is only written during construction; make it a get-only property",
        PrivateSetterOnlyWrittenDuringConstructionDescription);

    /// <summary>The PrivateSetterOnlyWrittenDuringConstruction rule description.</summary>
    private const string PrivateSetterOnlyWrittenDuringConstructionDescription =
        "An auto-property declared 'get; private set;' whose setter is only ever used inside a constructor or the property "
        + "initializer is telling the reader it can change after construction when it never does. Collapsing it to 'get;' says what "
        + "is actually true — the value is set once and then fixed — and the compiler enforces it: a stray later assignment stops "
        + "compiling instead of quietly mutating what everyone assumed was immutable. Reported only when the whole declaring type "
        + "has no assignment to the property outside a constructor of that type, so removing the setter cannot break a real write.";
}
