// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// The pieces SST2248 needs to rewrite two same-subject comparisons as one <c>is</c>-pattern: the
/// shared subject, each comparison's subject-on-left operator and constant, and whether the two were
/// joined by <c>&amp;&amp;</c> (an <c>and</c> pattern) or <c>||</c> (an <c>or</c> pattern).
/// </summary>
/// <param name="Subject">The bare local, field, or parameter read both comparisons test.</param>
/// <param name="LeftOperator">The left comparison kind, rewritten so the subject is on the left.</param>
/// <param name="LeftConstant">The constant the left comparison tests the subject against.</param>
/// <param name="RightOperator">The right comparison kind, rewritten so the subject is on the left.</param>
/// <param name="RightConstant">The constant the right comparison tests the subject against.</param>
/// <param name="IsConjunction"><see langword="true"/> for <c>&amp;&amp;</c>, <see langword="false"/> for <c>||</c>.</param>
internal readonly record struct ComparisonPatternMerge(
    ExpressionSyntax Subject,
    SyntaxKind LeftOperator,
    ExpressionSyntax LeftConstant,
    SyntaxKind RightOperator,
    ExpressionSyntax RightConstant,
    bool IsConjunction);
