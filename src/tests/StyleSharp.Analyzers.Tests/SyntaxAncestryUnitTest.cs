// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Tests the shared bounded ancestor walk.</summary>
public sealed class SyntaxAncestryUnitTest
{
    /// <summary>The source whose nodes the tests walk.</summary>
    private const string Source =
        """
        class C
        {
            void M(object gate)
            {
                lock (gate)
                {
                    return;
                }
            }
        }
        """;

    /// <summary>The parsed source.</summary>
    private static readonly CompilationUnitSyntax Root = SyntaxFactory.ParseCompilationUnit(Source);

    /// <summary>The <c>return</c> statement inside the lock.</summary>
    private static readonly ReturnStatementSyntax Return = Root.DescendantNodes().OfType<ReturnStatementSyntax>().Single();

    /// <summary>The <c>lock</c> statement.</summary>
    private static readonly LockStatementSyntax Lock = Root.DescendantNodes().OfType<LockStatementSyntax>().Single();

    /// <summary>The method holding the lock.</summary>
    private static readonly MethodDeclarationSyntax Method = Root.DescendantNodes().OfType<MethodDeclarationSyntax>().Single();

    /// <summary>Verifies an ancestor strictly between the node and the limit is found.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task FindsAnAncestorInsideTheLimitAsync() =>
        await Assert.That(SyntaxAncestry.HasAncestorBefore<LockStatementSyntax>(Return, Method)).IsTrue();

    /// <summary>Verifies the limit itself, and anything beyond it, is not tested.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task StopsAtTheLimitAsync()
    {
        await Assert.That(SyntaxAncestry.HasAncestorBefore<LockStatementSyntax>(Return, Lock)).IsFalse();
        await Assert.That(SyntaxAncestry.HasAncestorBefore<ClassDeclarationSyntax>(Return, Lock)).IsFalse();
    }

    /// <summary>Verifies the node itself is never counted as its own ancestor.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task IgnoresTheNodeItselfAsync() =>
        await Assert.That(SyntaxAncestry.HasAncestorBefore<LockStatementSyntax>(Lock, Method)).IsFalse();

    /// <summary>Verifies the predicate form finds an ancestor inside the limit that satisfies the test.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task PredicateFindsAMatchingAncestorInsideTheLimitAsync() =>
        await Assert.That(SyntaxAncestry.HasAncestorBefore(Return, Method, static node => node is LockStatementSyntax or BlockSyntax)).IsTrue();

    /// <summary>Verifies the predicate form tests neither the node, the limit, nor anything beyond it.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task PredicateStopsAtTheLimitAndSkipsTheNodeAsync()
    {
        await Assert.That(SyntaxAncestry.HasAncestorBefore(Return, Lock, static node => node is LockStatementSyntax or MethodDeclarationSyntax)).IsFalse();
        await Assert.That(SyntaxAncestry.HasAncestorBefore(Lock, Method, static node => node is LockStatementSyntax)).IsFalse();
    }
}
