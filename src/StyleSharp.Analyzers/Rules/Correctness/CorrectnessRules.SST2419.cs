// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>The SST2419 descriptor.</summary>
internal static partial class CorrectnessRules
{
    /// <summary>SST2419 — a set or list operation is applied to itself.</summary>
    public static readonly DiagnosticDescriptor SelfCollectionOperation = Create(
        "SST2419",
        "A collection operation should not be applied to itself",
        "'{0}' is called with the same collection as its receiver; {1}",
        SelfCollectionOperationDescription);

    /// <summary>The SelfCollectionOperation rule description.</summary>
    private const string SelfCollectionOperationDescription =
        "A set or list operation is given the very collection it is called on. Depending on the operation, the result is a no-op, a "
        + "constant, or a silent wipe: a set unioned or intersected with itself does not change, 'ExceptWith' against itself clears the "
        + "set, a subset or equality test against itself is always true, and a list that adds its own range doubles its contents. Each "
        + "of these reads as though it does something and almost always came from a copy-and-paste where the second argument should "
        + "have been a different collection. The rule works from the set and list interfaces, so it applies to every implementation, "
        + "and only when the receiver and argument are the same side-effect-free expression.";
}
