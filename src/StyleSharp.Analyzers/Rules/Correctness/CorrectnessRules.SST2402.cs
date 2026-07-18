// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>The SST2402 descriptor.</summary>
internal static partial class CorrectnessRules
{
    /// <summary>SST2402 — an instance member writes to state shared by every instance.</summary>
    public static readonly DiagnosticDescriptor StaticFieldWrittenInConstructor = Create(
        "SST2402",
        "Instance members should not write to static fields",
        "'{0}' is static; assigning it from an instance member lets every instance overwrite it for all the others",
        StaticFieldWrittenInConstructorDescription);

    /// <summary>The StaticFieldWrittenInConstructor rule description.</summary>
    private const string StaticFieldWrittenInConstructorDescription =
        "An instance member acts for one object, but a static field exists once per type. Assigning one from the other means the last "
        + "instance to run silently redefines the field for every object that already exists — and in a threaded program, which one "
        + "wins is a race. If the value belongs to the type, set it in a static constructor, an initializer, or a static member.";
}
