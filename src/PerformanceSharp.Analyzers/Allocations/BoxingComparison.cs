// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers;

/// <summary>An equality call whose type parameter operands box.</summary>
/// <param name="Left">The left operand.</param>
/// <param name="Right">The right operand.</param>
/// <param name="TypeParameter">The type parameter the operands share.</param>
internal readonly record struct BoxingComparison(ExpressionSyntax Left, ExpressionSyntax Right, ITypeParameterSymbol TypeParameter);
