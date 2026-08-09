// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>The shape a guarded member read folds into.</summary>
internal enum NullCheckFoldKind
{
    /// <summary>The conjunction is not a foldable shape.</summary>
    None,

    /// <summary>A bool-valued member read that folds to <c>receiver?.Member == true</c>.</summary>
    BooleanMember,

    /// <summary>A comparison against a non-null constant that folds to <c>receiver?.Member op constant</c>.</summary>
    Comparison,

    /// <summary>A <c>bool?</c> read through <c>.Value</c> that folds to <c>receiver == true</c>.</summary>
    NullableBooleanValue,
}
