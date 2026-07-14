// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>The SST2402 descriptor.</summary>
internal static partial class CorrectnessRules
{
    /// <summary>SST2402 — a constructor writes to state shared by every instance.</summary>
    public static readonly DiagnosticDescriptor StaticFieldWrittenInConstructor = Create(
        "SST2402",
        "Constructors should not write to static fields",
        "'{0}' is static; assigning it in an instance constructor lets every new instance overwrite it for all the others",
        StaticFieldWrittenInConstructorDescription);

    /// <summary>The StaticFieldWrittenInConstructor rule description.</summary>
    private const string StaticFieldWrittenInConstructorDescription =
        "An instance constructor runs once per object, but a static field exists once per type. Assigning one from the other means the "
        + "last object constructed silently redefines the field for every object that already exists — and in a threaded program, which one "
        + "wins is a race. If the value belongs to the type, set it in a static constructor or an initializer.";
}
