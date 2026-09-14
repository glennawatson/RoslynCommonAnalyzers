// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>The local declared immediately above a loop that counts it.</summary>
/// <param name="Declaration">The declaration statement, or <see langword="null"/> when the shape does not match.</param>
/// <param name="Declarator">The declaration's one declarator, or <see langword="null"/> when the shape does not match.</param>
internal readonly record struct CounterDeclaration(LocalDeclarationStatementSyntax? Declaration, VariableDeclaratorSyntax? Declarator);
