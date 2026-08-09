// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>The SST2466 descriptor.</summary>
internal static partial class CorrectnessRules
{
    /// <summary>SST2466 — a <c>finally</c> clause with no statements guarantees nothing.</summary>
    public static readonly DiagnosticDescriptor EmptyFinallyClause = Create(
        "SST2466",
        "An empty finally clause should be removed",
        "This 'finally' runs no cleanup, so the 'try' guarantees nothing",
        EmptyFinallyClauseDescription);

    /// <summary>The EmptyFinallyClause rule description.</summary>
    private const string EmptyFinallyClauseDescription =
        "A 'finally' exists to run cleanup whatever happens. An empty one runs nothing, so the 'try' it is "
        + "attached to buys only the cost of the exception region — and, worse, it reads as though unwinding is "
        + "handled when it is not. The usual history is cleanup that moved elsewhere, or a block someone opened "
        + "and never filled. Removing the clause leaves a plain 'try/catch', or plain statements when there is "
        + "no 'catch' either. Reported only when the clause has no statements at all; a clause holding only "
        + "comments is left alone, because the comment may be the note explaining why nothing runs.";
}
