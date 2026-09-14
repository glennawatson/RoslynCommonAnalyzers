// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers;

/// <summary>The receiver and key of a <c>!receiver.Method(key)</c> guard.</summary>
/// <param name="Receiver">The guard receiver.</param>
/// <param name="Key">The guard key.</param>
internal readonly record struct NegatedGuard(ExpressionSyntax Receiver, ExpressionSyntax Key);
