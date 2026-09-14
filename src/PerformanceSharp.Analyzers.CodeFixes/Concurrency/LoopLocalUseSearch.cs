// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers;

/// <summary>The inputs a search for uses of a loop local outside the loop body reads.</summary>
/// <param name="Body">The loop body.</param>
/// <param name="Local">The local being moved.</param>
/// <param name="Model">The semantic model.</param>
/// <param name="Ignore">The reported identifier, always inside the loop.</param>
/// <param name="CancellationToken">A token that cancels binding.</param>
internal readonly record struct LoopLocalUseSearch(BlockSyntax Body, ILocalSymbol Local, SemanticModel Model, IdentifierNameSyntax Ignore, CancellationToken CancellationToken);
