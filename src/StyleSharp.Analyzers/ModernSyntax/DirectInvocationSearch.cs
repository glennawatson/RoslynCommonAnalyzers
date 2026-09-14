// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>The local delegate a direct-invocation search binds against, and whether a reference was seen.</summary>
/// <param name="Symbol">The local delegate.</param>
/// <param name="Model">The semantic model.</param>
/// <param name="CancellationToken">A token that cancels binding.</param>
internal record struct DirectInvocationSearch(ILocalSymbol Symbol, SemanticModel Model, CancellationToken CancellationToken)
{
    /// <summary>Gets or sets a value indicating whether a reference to the local has been seen.</summary>
    public bool SeenReference { get; set; }
}
