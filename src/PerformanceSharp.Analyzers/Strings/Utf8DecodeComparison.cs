// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers;

/// <summary>The operands of a comparison between a UTF-8 decode and another value.</summary>
/// <param name="Decode">The <c>Encoding.UTF8.GetString</c> invocation.</param>
/// <param name="Constant">The other operand.</param>
internal readonly record struct Utf8DecodeComparison(InvocationExpressionSyntax Decode, ExpressionSyntax Constant);
