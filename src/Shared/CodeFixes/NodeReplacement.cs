// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace RoslynCommon.Analyzers.CodeFixes;

/// <summary>A single-node edit computed by a code fix: the reported node and its replacement.</summary>
/// <param name="Original">The node being replaced.</param>
/// <param name="Replacement">The replacement node.</param>
internal readonly record struct NodeReplacement(SyntaxNode Original, SyntaxNode Replacement);
