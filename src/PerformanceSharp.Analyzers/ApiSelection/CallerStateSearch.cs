// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers;

/// <summary>The inputs a search for per-caller state inside an options construction reads.</summary>
/// <param name="Model">The semantic model.</param>
/// <param name="CancellationToken">A token that cancels binding.</param>
internal readonly record struct CallerStateSearch(SemanticModel Model, CancellationToken CancellationToken);
