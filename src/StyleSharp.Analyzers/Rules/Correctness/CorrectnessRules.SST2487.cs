// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>The SST2487 descriptor.</summary>
internal static partial class CorrectnessRules
{
    /// <summary>SST2487 — a <c>[ConstructorArgument]</c> names no parameter of any constructor of its type.</summary>
    public static readonly DiagnosticDescriptor ConstructorArgumentMismatch = Create(
        "SST2487",
        "A [ConstructorArgument] should name a constructor parameter",
        "'{0}' matches no parameter of any constructor of '{1}', so the markup extension cannot round-trip this property back to a constructor argument",
        ConstructorArgumentMismatchDescription);

    /// <summary>The ConstructorArgumentMismatch rule description.</summary>
    private const string ConstructorArgumentMismatchDescription =
        "A markup extension carries the argument a property was built from in a [ConstructorArgument] on that property: the name in "
        + "the attribute is matched against the extension's constructor parameters so a serializer can write the value back out as a "
        + "positional argument. The name is a string the compiler never checks, so a typo or a renamed parameter leaves it pointing "
        + "at nothing. Nothing fails at build time; the property simply stops round-tripping, and the value is dropped or emitted in "
        + "a longer property form the next time the markup is written. The name in the attribute must match a parameter of one of the "
        + "declaring type's constructors, compared exactly.";
}
