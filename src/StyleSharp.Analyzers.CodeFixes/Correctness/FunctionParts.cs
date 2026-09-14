// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>The modifiers and block body of a method or local function.</summary>
/// <param name="Modifiers">The function's modifiers.</param>
/// <param name="Body">The block body, or <see langword="null"/> when the function is expression-bodied.</param>
internal readonly record struct FunctionParts(SyntaxTokenList Modifiers, BlockSyntax? Body);
