// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>The SST2428 descriptor.</summary>
internal static partial class CorrectnessRules
{
    /// <summary>SST2428 — a static field initializer reads a static field that runs after it.</summary>
    public static readonly DiagnosticDescriptor StaticInitializerReadsLaterField = Create(
        "SST2428",
        "A static field initializer should not read a static field declared later",
        "'{0}' reads '{1}', {2}",
        StaticInitializerReadsLaterFieldDescription);

    /// <summary>The StaticInitializerReadsLaterField rule description.</summary>
    private const string StaticInitializerReadsLaterFieldDescription =
        "Static field initializers run in textual declaration order. An initializer that reads a static field declared later in the same "
        + "type sees that field's default — null for a reference type, zero for a number — and keeps it, permanently and silently, because "
        + "the value it wanted had not been assigned yet when the initializer ran. Move the field it depends on above it, or compute both in "
        + "a static constructor where the order is under your control. When the two fields live in different files of the same partial type, "
        + "which initializer runs first is undefined, so the read can see the default no matter how the declarations are ordered.";
}
