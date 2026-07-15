// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Tests the shared enum-switch coverage building blocks.</summary>
public sealed class EnumSwitchCoverageUnitTest
{
    /// <summary>The source whose enum switch the tests inspect.</summary>
    private const string Source =
        """
        enum E { A, B, C }
        class C
        {
            void M(E e)
            {
                switch (e)
                {
                    case E.A:
                        break;
                    case E.B:
                        break;
                }
            }
        }
        """;

    /// <summary>Verifies enum value fields are recognized and other members are not.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task IsEnumValueRecognizesEnumFieldsOnlyAsync()
    {
        var (root, model) = SemanticModelFactory.Create(Source);
        var enumType = (INamedTypeSymbol)model.GetDeclaredSymbol((EnumDeclarationSyntax)root.Members[0])!;
        var classType = (INamedTypeSymbol)model.GetDeclaredSymbol((ClassDeclarationSyntax)root.Members[1])!;

        await Assert.That(EnumSwitchCoverage.IsEnumValue(enumType.GetMembers("A")[0], out var field)).IsTrue();
        await Assert.That(field.Name).IsEqualTo("A");
        await Assert.That(EnumSwitchCoverage.IsEnumValue(classType.GetMembers("M")[0], out _)).IsFalse();
    }

    /// <summary>Verifies a covered value is detected and a missing one is not.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task IsCaseLabelCoveredDistinguishesPresentAndMissingValuesAsync()
    {
        var (root, model) = SemanticModelFactory.Create(Source);
        var enumType = (INamedTypeSymbol)model.GetDeclaredSymbol((EnumDeclarationSyntax)root.Members[0])!;
        var switchStatement = FirstDescendant<SwitchStatementSyntax>(root);
        var covered = (IFieldSymbol)enumType.GetMembers("A")[0];
        var missing = (IFieldSymbol)enumType.GetMembers("C")[0];

        await Assert.That(EnumSwitchCoverage.IsCaseLabelCovered(covered, switchStatement, model, CancellationToken.None)).IsTrue();
        await Assert.That(EnumSwitchCoverage.IsCaseLabelCovered(missing, switchStatement, model, CancellationToken.None)).IsFalse();
    }

    /// <summary>Finds the first descendant node of the requested type.</summary>
    /// <typeparam name="T">The node type.</typeparam>
    /// <param name="root">The root to search.</param>
    /// <returns>The first matching descendant.</returns>
    private static T FirstDescendant<T>(SyntaxNode root)
        where T : SyntaxNode
    {
        foreach (var node in root.DescendantNodes())
        {
            if (node is T match)
            {
                return match;
            }
        }

        throw new InvalidOperationException($"No {typeof(T).Name} was found.");
    }
}
