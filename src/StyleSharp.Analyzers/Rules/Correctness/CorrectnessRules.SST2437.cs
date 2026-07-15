// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>The SST2437 descriptor.</summary>
internal static partial class CorrectnessRules
{
    /// <summary>SST2437 — a generic type inherits from a base that instantiates the type with itself.</summary>
    public static readonly DiagnosticDescriptor RecursiveGenericInheritance = Create(
        "SST2437",
        "Avoid recursive generic inheritance",
        "'{0}' inherits from a base type that nests '{0}' inside its own type arguments, which expands without end and throws a TypeLoadException when the type loads",
        RecursiveGenericInheritanceDescription);

    /// <summary>The RecursiveGenericInheritance rule description.</summary>
    private const string RecursiveGenericInheritanceDescription =
        "A generic type whose base type instantiates the type with a type argument that again contains the type - for example a type C that "
        + "derives from Base<C<C<T>>> - forces the runtime to build an ever-larger chain of constructed types when it loads C. The source "
        + "compiles clean and reports nothing, then the type throws a TypeLoadException the moment it is loaded, and any tool that walks the "
        + "constructed type graph runs out of stack. The self-referential base used for fluent builders and comparable types (C deriving from "
        + "Base<C>) is fine and is not reported; only a type nested inside its own base's type arguments is.";
}
