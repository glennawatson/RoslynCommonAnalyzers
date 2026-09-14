// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>The inputs a search for references to a local outside a proposed inline scope reads.</summary>
/// <param name="InlineScope">The block that would hold the inline declaration.</param>
/// <param name="Local">The local being moved.</param>
/// <param name="Model">The semantic model.</param>
/// <param name="CancellationToken">A token that cancels binding.</param>
internal readonly record struct InlineScopeReferenceSearch(SyntaxNode InlineScope, ISymbol Local, SemanticModel Model, CancellationToken CancellationToken);
