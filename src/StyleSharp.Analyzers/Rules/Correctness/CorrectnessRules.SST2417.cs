// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>The SST2417 descriptor.</summary>
internal static partial class CorrectnessRules
{
    /// <summary>SST2417 — an assignment operator is spaced like a transposed compound assignment.</summary>
    public static readonly DiagnosticDescriptor TransposedCompoundAssignment = Create(
        "SST2417",
        "An assignment should not read as a transposed operator",
        "'={0}' is spaced like a transposed '{0}=' operator",
        TransposedCompoundAssignmentDescription);

    /// <summary>The TransposedCompoundAssignment rule description.</summary>
    private const string TransposedCompoundAssignmentDescription =
        "The '=' is written flush against a following unary '+', '-', or '!', with a space after that operator ('x =+ 1'). The spacing "
        + "mimics a compound assignment whose two characters were typed in the wrong order: 'x =+ 1' assigns the positive value '+1', "
        + "but reads as the '+=' the author almost certainly meant. The tokens for 'x =+ 1' and 'x = +1' are the same and differ only "
        + "in where the space falls, so the whitespace asymmetry is the whole signal. Either close the gap into the compound operator "
        + "('x += 1') or open it so the unary intent is plain ('x = +1').";
}
