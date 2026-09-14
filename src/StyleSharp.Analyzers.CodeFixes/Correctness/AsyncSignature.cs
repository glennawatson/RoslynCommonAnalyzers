// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>A function's modifiers with <c>async</c> added and its return type.</summary>
/// <param name="Modifiers">The modifiers including <c>async</c>.</param>
/// <param name="ReturnType">The return type, with its leading trivia moved to <c>async</c> when it led the declaration.</param>
internal readonly record struct AsyncSignature(SyntaxTokenList Modifiers, TypeSyntax ReturnType);
