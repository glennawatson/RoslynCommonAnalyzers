// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Text;

namespace StyleSharp.Analyzers;

/// <summary>The span an expression-body collapse keeps, and where the collapsed container ends.</summary>
/// <param name="ExpressionSpan">The kept expression's span, whose inner trivia survives the collapse.</param>
/// <param name="ContainerEnd">The end of the block or accessor list being collapsed.</param>
internal readonly record struct CommentLossScan(TextSpan ExpressionSpan, int ContainerEnd);
