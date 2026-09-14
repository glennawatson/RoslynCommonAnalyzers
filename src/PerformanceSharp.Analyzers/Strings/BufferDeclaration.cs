// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers;

/// <summary>A local char buffer copied from a string, and the block it lives in.</summary>
/// <param name="Declarator">The buffer's declarator.</param>
/// <param name="Block">The block holding the declaration.</param>
internal readonly record struct BufferDeclaration(VariableDeclaratorSyntax Declarator, BlockSyntax Block);
