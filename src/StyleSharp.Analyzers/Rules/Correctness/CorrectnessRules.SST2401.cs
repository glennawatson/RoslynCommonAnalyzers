// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>The SST2401 descriptor.</summary>
internal static partial class CorrectnessRules
{
    /// <summary>SST2401 — a null-dereference failure is caught rather than prevented.</summary>
    public static readonly DiagnosticDescriptor CatchNullReference = Create(
        "SST2401",
        "Do not catch NullReferenceException",
        "Catching '{0}' hides a bug rather than handling a failure",
        CatchNullReferenceDescription);

    /// <summary>The CatchNullReference rule description.</summary>
    private const string CatchNullReferenceDescription =
        "A NullReferenceException is not a condition to recover from; it is the runtime reporting that the code dereferenced something it "
        + "never checked. Catching it converts a crash with a stack trace into silent, arbitrary behavior — and it catches every future "
        + "instance of the same mistake, anywhere inside the try block. Guard the value instead.";
}
