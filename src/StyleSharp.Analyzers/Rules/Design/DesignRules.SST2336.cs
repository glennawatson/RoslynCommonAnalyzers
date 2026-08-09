// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>The SST2336 descriptor.</summary>
internal static partial class DesignRules
{
    /// <summary>SST2336 — an attribute type does not say where it may be applied.</summary>
    public static readonly DiagnosticDescriptor MissingAttributeUsage = Create(
        "SST2336",
        "An attribute type should declare where it can be applied",
        "Declare [AttributeUsage] on '{0}' so its targets and repeatability are stated",
        MissingAttributeUsageDescription);

    /// <summary>The MissingAttributeUsage rule description.</summary>
    private const string MissingAttributeUsageDescription =
        "Without '[AttributeUsage]' an attribute defaults to 'AttributeTargets.All', single-use, and "
        + "non-inherited. Almost no attribute means all of that: one written for methods can be put on a "
        + "parameter or an assembly and the compiler will not object, so the mistake surfaces as the attribute "
        + "quietly doing nothing rather than as a build error. Declaring the usage turns every wrong placement "
        + "into a compile error, and is the only way to say that an attribute may repeat or that derived types "
        + "should inherit it. Reported for a non-abstract type deriving from 'Attribute' that declares no "
        + "'[AttributeUsage]' of its own; an abstract base is left alone, because the concrete attribute that "
        + "derives from it is where the targets belong.";
}
