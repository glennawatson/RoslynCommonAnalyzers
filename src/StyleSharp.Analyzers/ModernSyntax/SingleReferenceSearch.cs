// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>The local a single-reference search binds against, and the reference found so far.</summary>
/// <param name="Model">The semantic model.</param>
/// <param name="Local">The local being searched for.</param>
/// <param name="CancellationToken">A token that cancels binding.</param>
internal record struct SingleReferenceSearch(SemanticModel Model, ILocalSymbol Local, CancellationToken CancellationToken)
{
    /// <summary>Gets or sets the one reference found so far, or <see langword="null"/> when none has been found.</summary>
    public IdentifierNameSyntax? Reference { get; set; }
}
