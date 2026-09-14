// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace RoslynCommon.Analyzers;

/// <summary>A syntax node action that receives per-compilation state, paired with the syntax kinds it runs for.</summary>
/// <typeparam name="TState">The per-compilation state type.</typeparam>
/// <param name="Action">Analyzes each matching node.</param>
/// <param name="Kinds">The syntax kinds the action runs for.</param>
internal readonly record struct SyntaxNodeRegistration<TState>(ActionIn<SyntaxNodeAnalysisContext, TState> Action, SyntaxKind[] Kinds);
