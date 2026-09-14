// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Tests the bind that confirms which type declares an invoked method.</summary>
public class InvocationTargetsTests
{
    /// <summary>The source every invocation test binds against.</summary>
    private const string Source = """
        class Console { public static void WriteLine(string text) { } }

        class Program
        {
            void Run()
            {
                System.Console.WriteLine("framework");
                Console.WriteLine("local");
                Missing.WriteLine("unbound");
            }
        }
        """;

    /// <summary>Verifies only a call that binds to the type's own method matches.</summary>
    /// <param name="callIndex">The position of the call in <c>Run</c>.</param>
    /// <param name="expected">Whether the call binds to a method of <c>System.Console</c>.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments(0, true)]
    [Arguments(1, false)]
    [Arguments(2, false)]
    public async Task MatchesOnlyTheDeclaringTypeAsync(int callIndex, bool expected)
    {
        var (root, model) = SemanticModelFactory.Create(Source);
        var calls = root.DescendantNodes().OfType<InvocationExpressionSyntax>().ToArray();
        var console = model.Compilation.GetTypeByMetadataName("System.Console")!;

        await Assert.That(InvocationTargets.IsMethodOf(model, calls[callIndex], console, CancellationToken.None)).IsEqualTo(expected);
    }
}
