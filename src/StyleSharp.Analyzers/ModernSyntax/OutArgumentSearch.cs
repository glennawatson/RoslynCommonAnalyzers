// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>The local an out-argument search binds against, and the one matching argument found so far.</summary>
/// <param name="Boundary">The statement the search stays inside.</param>
/// <param name="Local">The local being passed.</param>
/// <param name="Model">The semantic model.</param>
/// <param name="CancellationToken">A token that cancels binding.</param>
internal record struct OutArgumentSearch(StatementSyntax Boundary, ISymbol Local, SemanticModel Model, CancellationToken CancellationToken)
{
    /// <summary>Gets or sets the matching out argument, or <see langword="null"/> when none has been found.</summary>
    public ArgumentSyntax? Match { get; set; }
}
