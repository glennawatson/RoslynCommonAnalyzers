// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>The line breaks around both operators of a conditional expression.</summary>
/// <param name="QuestionBefore">Whether a line break precedes <c>?</c>.</param>
/// <param name="QuestionAfter">Whether a line break follows <c>?</c>.</param>
/// <param name="ColonBefore">Whether a line break precedes <c>:</c>.</param>
/// <param name="ColonAfter">Whether a line break follows <c>:</c>.</param>
internal readonly record struct ConditionalLineBreaks(bool QuestionBefore, bool QuestionAfter, bool ColonBefore, bool ColonAfter);
