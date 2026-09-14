// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace RoslynCommon.Analyzers;

/// <summary>The symbol an identifier search binds each same-named identifier against.</summary>
/// <param name="Symbol">The symbol to find.</param>
/// <param name="Model">The semantic model that binds each identifier.</param>
/// <param name="CancellationToken">A token that cancels binding.</param>
internal readonly record struct SymbolReferenceSearch(ISymbol Symbol, SemanticModel Model, CancellationToken CancellationToken);
