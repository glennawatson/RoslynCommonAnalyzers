// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Operations;

namespace SecuritySharp.Analyzers;

/// <summary>The bound arguments of a call or construction and the type declaring its target.</summary>
/// <param name="Arguments">The bound argument operations, or a default array when the node does not bind.</param>
/// <param name="ContainingType">The type declaring the called member, or <see langword="null"/> when the node does not bind.</param>
internal readonly record struct BoundCall(ImmutableArray<IArgumentOperation> Arguments, INamedTypeSymbol? ContainingType);
