// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>The SST2307 descriptor.</summary>
internal static partial class DesignRules
{
    /// <summary>SST2307 — a generic method has a type parameter no argument can pin down.</summary>
    public static readonly DiagnosticDescriptor InferableTypeParameter = Create(
        "SST2307",
        "Generic method type parameters should be inferable from the parameters",
        "'{0}' on '{1}' appears in no parameter, so every caller has to name it",
        InferableTypeParameterDescription);

    /// <summary>The InferableTypeParameter rule description.</summary>
    private const string InferableTypeParameterDescription =
        "C# infers a method's type arguments from the arguments it is given, and from nothing else — not the return type, not the "
        + "constraints. A type parameter that appears in no parameter therefore cannot be inferred, and every single call site has to spell "
        + "the type out. Take the type parameter as a parameter, or drop it and let the caller pass the value it describes.";
}
