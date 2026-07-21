// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>The SST2495 descriptor.</summary>
internal static partial class CorrectnessRules
{
    /// <summary>SST2495 — one operand of a flags combination is already covered by another, so it adds nothing.</summary>
    public static readonly DiagnosticDescriptor RedundantFlagsOperand = Create(
        "SST2495",
        "A flags combination should not repeat bits another operand already sets",
        "'{0}' sets no bit that '{1}' does not already set, so it contributes nothing to this combination",
        RedundantFlagsOperandDescription);

    /// <summary>The RedundantFlagsOperand rule description.</summary>
    private const string RedundantFlagsOperandDescription =
        "A '|' combination of [Flags] enum members includes an operand whose bits are all already set by another operand "
        + "in the same expression. Because the value of every operand is a compile-time constant, the redundant one can be "
        + "proven to change nothing: or-ing it in leaves the result identical. Usually the repeated operand is a symptom — "
        + "a composite member that overlaps a single flag listed beside it, or a copy-paste of the wrong member — and the "
        + "intent was a different flag. Removing the operand that adds nothing keeps the value the same and makes the "
        + "combination say what it computes; if a different flag was meant, that is the real fix.";
}
