// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>The SST2403 descriptor.</summary>
internal static partial class CorrectnessRules
{
    /// <summary>SST2403 — a half-built instance escapes its own constructor.</summary>
    public static readonly DiagnosticDescriptor ThisEscapesConstructor = Create(
        "SST2403",
        "Do not let 'this' escape from a constructor",
        "'this' escapes '{0}' before construction finishes; the receiver can observe a half-built object",
        ThisEscapesConstructorDescription);

    /// <summary>The ThisEscapesConstructor rule description.</summary>
    private const string ThisEscapesConstructorDescription =
        "Handing 'this' to anything before the constructor returns publishes an object whose fields are not all set — including 'readonly' "
        + "ones, which the receiver may read as null. Worse, if the receiver stores it somewhere another thread can see, that thread can "
        + "use the object while it is still being built. Publish the instance after construction, from a factory or an initialization step.";
}
