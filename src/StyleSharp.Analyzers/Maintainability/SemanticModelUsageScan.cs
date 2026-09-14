// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Concurrent;

namespace StyleSharp.Analyzers;

/// <summary>The semantic model walked for private member declarations, and the usage shared by partial types.</summary>
/// <param name="Context">The semantic model analysis context.</param>
/// <param name="Usages">The usage accumulated for each partial type symbol.</param>
internal readonly record struct SemanticModelUsageScan(SemanticModelAnalysisContext Context, ConcurrentDictionary<INamedTypeSymbol, PrivateTypeUsage> Usages);
