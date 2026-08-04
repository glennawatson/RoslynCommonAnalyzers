// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>The SST2498 descriptor.</summary>
internal static partial class CorrectnessRules
{
    /// <summary>SST2498 — <c>nameof</c> on a type parameter yields the parameter's own name, not the type argument's.</summary>
    public static readonly DiagnosticDescriptor NameofTypeParameter = Create(
        "SST2498",
        "Do not use nameof on a type parameter",
        "'nameof({0})' is the constant \"{0}\", not the name of the type argument; use 'typeof({0}).Name'",
        NameofTypeParameterDescription);

    /// <summary>The NameofTypeParameter rule description.</summary>
    private const string NameofTypeParameterDescription =
        "nameof is resolved by the compiler, so nameof(T) on a type parameter is the literal text \"T\" in every "
        + "instantiation — it never becomes the name of the type substituted for T. Where the value is shown to a "
        + "person the result is a message that reads 'of type T', and where it is used as data the consequences are "
        + "worse: every instantiation contributes the same fragment, so values meant to be distinct per type collide. "
        + "A file name built this way is one file shared by every type, and a cache key built this way is one key. Use "
        + "typeof(T).Name for the substituted type's name, or typeof(T).FullName where the namespace has to "
        + "disambiguate it.";
}
