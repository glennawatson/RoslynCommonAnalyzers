// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers;

/// <summary>A comparison guard read with the checked value on the left.</summary>
/// <param name="Value">The checked value.</param>
/// <param name="Operand">The other operand.</param>
/// <param name="Kind">The comparison kind, mirrored when the operands were swapped.</param>
internal readonly record struct OrientedComparison(IdentifierNameSyntax Value, ExpressionSyntax Operand, SyntaxKind Kind);
