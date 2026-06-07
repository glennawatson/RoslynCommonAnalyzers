// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Tests shared field-reference helper fast paths used by lock-target analysis.</summary>
public class FieldReferenceAnalysisUnitTest
{
    /// <summary>Verifies the shared private-object-field check recognizes a simple lock target.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task PrivateObjectFieldLockTargetCheckRecognizesPrivateObjectField()
    {
        var lockStatement = ParseLockStatement(
            "public class C { private readonly object _gate = new(); void M() { lock (_gate) { } } }");
        var type = (TypeDeclarationSyntax)lockStatement.Parent!.Parent!.Parent!;

        await Assert.That(FieldReferenceAnalysis.IsPrivateObjectFieldLockTarget(type, lockStatement.Expression)).IsTrue();
    }

    /// <summary>Parses the first lock statement from the supplied source.</summary>
    /// <param name="source">The source containing the lock statement.</param>
    /// <returns>The parsed lock statement.</returns>
    private static LockStatementSyntax ParseLockStatement(string source)
        => SyntaxFactory.ParseCompilationUnit(source).DescendantNodes().OfType<LockStatementSyntax>().Single();
}
