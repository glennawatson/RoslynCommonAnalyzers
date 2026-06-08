// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>Shared syntax-trivia helpers for spotting line breaks directly from token trivia.</summary>
internal static class TriviaLineBreakHelper
{
    /// <summary>Returns whether the trivia list contains an end-of-line marker.</summary>
    /// <param name="trivia">The trivia list to inspect.</param>
    /// <returns><see langword="true"/> when the trivia includes a line break.</returns>
    public static bool HasLineBreak(SyntaxTriviaList trivia)
    {
        for (var i = 0; i < trivia.Count; i++)
        {
            if (trivia[i].IsKind(SyntaxKind.EndOfLineTrivia))
            {
                return true;
            }
        }

        return false;
    }
}
