// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers;

/// <summary>The symbols that exempt a blocking wait from the rule.</summary>
/// <param name="NotifyCompletion">The awaiter marker interface, or <see langword="null"/> when absent.</param>
/// <param name="EntryPoint">The compilation's entry point, or <see langword="null"/> when there is none.</param>
internal readonly record struct BlockingWaitExemptions(INamedTypeSymbol? NotifyCompletion, IMethodSymbol? EntryPoint);
