// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>A local declaration whose pure initializer can be inlined into its single use.</summary>
/// <param name="Block">The block holding the declaration.</param>
/// <param name="Declarator">The local's declarator.</param>
/// <param name="Value">The initializer that would be inlined.</param>
internal readonly record struct InlinableLocal(BlockSyntax Block, VariableDeclaratorSyntax Declarator, ExpressionSyntax Value);
