// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>An operator's expression kind, token kind and source text.</summary>
/// <param name="ExpressionKind">The expression kind the operator produces.</param>
/// <param name="TokenKind">The operator token kind.</param>
/// <param name="Text">The operator as written in source.</param>
internal readonly record struct OperatorForm(SyntaxKind ExpressionKind, SyntaxKind TokenKind, string Text);
