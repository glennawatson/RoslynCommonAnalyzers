// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers;

/// <summary>A counting operand and whether its comparison means the sequence has elements.</summary>
/// <typeparam name="TCount">The node type the comparison counts.</typeparam>
/// <param name="Count">The counting operand.</param>
/// <param name="HasElements">Whether the comparison means the sequence has elements.</param>
internal readonly record struct EmptinessComparison<TCount>(TCount Count, bool HasElements);
