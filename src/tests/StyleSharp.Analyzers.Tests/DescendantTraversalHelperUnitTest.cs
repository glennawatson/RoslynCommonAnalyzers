// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for preorder descendant traversal helpers.</summary>
public sealed class DescendantTraversalHelperUnitTest
{
    /// <summary>Verifies matching descendants are visited in source preorder.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task VisitDescendantsCollectsMatchesInPreorder()
    {
        var type = ParseType(
            "public class Outer { void First() { } class Inner { void Nested() { } } void Second() { } }");
        var names = new List<string>();

        var completed = DescendantTraversalHelper.VisitDescendants<MethodDeclarationSyntax, List<string>>(
            type,
            ref names,
            CollectMethodNames);

        await Assert.That(completed).IsTrue();
        await Assert.That(string.Join(",", names)).IsEqualTo("First,Nested,Second");
    }

    /// <summary>Verifies the visitor can stop the traversal early.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task VisitDescendantsStopsEarlyWhenVisitorReturnsFalse()
    {
        var type = ParseType("public class Outer { void First() { } void Second() { } }");
        var names = new List<string>();

        var completed = DescendantTraversalHelper.VisitDescendants<MethodDeclarationSyntax, List<string>>(
            type,
            ref names,
            CollectFirstMethodAndStop);

        await Assert.That(completed).IsFalse();
        await Assert.That(string.Join(",", names)).IsEqualTo("First");
    }

    /// <summary>Verifies the root node itself is not reported as a descendant match.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task VisitDescendantsDoesNotMatchTheRootNode()
    {
        var type = ParseType("public class Outer { class Inner { } }");
        var names = new List<string>();

        var completed = DescendantTraversalHelper.VisitDescendants<TypeDeclarationSyntax, List<string>>(
            type,
            ref names,
            CollectTypeNames);

        await Assert.That(completed).IsTrue();
        await Assert.That(string.Join(",", names)).IsEqualTo("Inner");
    }

    /// <summary>Parses a single type declaration for traversal helper tests.</summary>
    /// <param name="source">The source to parse.</param>
    /// <returns>The parsed type declaration.</returns>
    private static TypeDeclarationSyntax ParseType(string source)
        => (TypeDeclarationSyntax)SyntaxFactory.ParseCompilationUnit(source).Members[0];

    /// <summary>Adds one method name and keeps traversing.</summary>
    /// <param name="method">The visited method declaration.</param>
    /// <param name="collectedNames">The collected method names.</param>
    /// <returns><see langword="true"/> to continue the traversal.</returns>
    private static bool CollectMethodNames(MethodDeclarationSyntax method, ref List<string> collectedNames)
    {
        collectedNames.Add(method.Identifier.ValueText);
        return true;
    }

    /// <summary>Adds the first method name and stops traversing.</summary>
    /// <param name="method">The visited method declaration.</param>
    /// <param name="collectedNames">The collected method names.</param>
    /// <returns><see langword="false"/> to stop the traversal.</returns>
    private static bool CollectFirstMethodAndStop(MethodDeclarationSyntax method, ref List<string> collectedNames)
    {
        collectedNames.Add(method.Identifier.ValueText);
        return false;
    }

    /// <summary>Adds one type name and keeps traversing.</summary>
    /// <param name="declaration">The visited type declaration.</param>
    /// <param name="collectedNames">The collected type names.</param>
    /// <returns><see langword="true"/> to continue the traversal.</returns>
    private static bool CollectTypeNames(TypeDeclarationSyntax declaration, ref List<string> collectedNames)
    {
        collectedNames.Add(declaration.Identifier.ValueText);
        return true;
    }
}
