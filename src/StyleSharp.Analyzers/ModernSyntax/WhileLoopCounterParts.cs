// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>The parts of a while loop that a for header would gather.</summary>
/// <param name="Declaration">The counter declaration sitting immediately above the loop.</param>
/// <param name="Incrementor">The statement stepping the counter at the end of the body.</param>
/// <param name="CounterName">The counter's declared name.</param>
internal readonly record struct WhileLoopCounterParts(
    LocalDeclarationStatementSyntax Declaration,
    ExpressionStatementSyntax Incrementor,
    string CounterName);
