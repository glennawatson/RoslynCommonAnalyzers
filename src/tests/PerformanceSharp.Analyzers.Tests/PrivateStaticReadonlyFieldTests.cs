// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace PerformanceSharp.Analyzers.Tests;

/// <summary>Tests the declaration, scope, and reference checks for a private static readonly field.</summary>
public class PrivateStaticReadonlyFieldTests
{
    /// <summary>The uses of <c>A</c> in the reference fixture: <c>A</c>, <c>C.A</c>, and <c>nameof(A)</c>.</summary>
    private const int ExpectedReferenceCount = 3;

    /// <summary>Verifies only a single declarator with an initializer counts.</summary>
    /// <param name="declaration">The field declaration.</param>
    /// <param name="expected">Whether the declaration is a single initialized variable.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("private static readonly int A = 1;", true)]
    [Arguments("private static readonly int A = 1, B = 2;", false)]
    [Arguments("private static readonly int A;", false)]
    public async Task SingleInitializedVariableRequiresOneInitializedDeclaratorAsync(string declaration, bool expected)
    {
        var field = ParseField(declaration);

        await Assert.That(PrivateStaticReadonlyField.IsSingleInitializedVariable(field)).IsEqualTo(expected);
    }

    /// <summary>Verifies the modifiers must make the field private, static, and readonly.</summary>
    /// <param name="declaration">The field declaration.</param>
    /// <param name="expected">Whether the modifier shape matches.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("private static readonly int A = 1;", true)]
    [Arguments("static readonly int A = 1;", true)]
    [Arguments("readonly static private int A = 1;", true)]
    [Arguments("public static readonly int A = 1;", false)]
    [Arguments("internal static readonly int A = 1;", false)]
    [Arguments("protected static readonly int A = 1;", false)]
    [Arguments("private protected static readonly int A = 1;", false)]
    [Arguments("private static int A = 1;", false)]
    [Arguments("private readonly int A = 1;", false)]
    public async Task ModifiersMustBePrivateStaticReadonlyAsync(string declaration, bool expected)
    {
        var field = ParseField(declaration);

        await Assert.That(PrivateStaticReadonlyField.HasPrivateStaticReadonlyModifiers(field)).IsEqualTo(expected);
    }

    /// <summary>Verifies the containing type is returned only when no other part of it exists.</summary>
    /// <param name="typeDeclaration">A type declaration holding one field.</param>
    /// <param name="expected">Whether the containing type is returned.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("class C { static readonly int A = 1; }", true)]
    [Arguments("struct C { static readonly int A = 1; }", true)]
    [Arguments("record C { static readonly int A = 1; }", true)]
    [Arguments("partial class C { static readonly int A = 1; }", false)]
    public async Task ContainingTypeMustNotBePartialAsync(string typeDeclaration, bool expected)
    {
        var field = SyntaxFactory.ParseCompilationUnit(typeDeclaration).DescendantNodes().OfType<FieldDeclarationSyntax>().Single();

        await Assert.That(PrivateStaticReadonlyField.TryGetNonPartialContainingType(field, out var containingType)).IsEqualTo(expected);
        await Assert.That(containingType is not null).IsEqualTo(expected);
    }

    /// <summary>Verifies a field without a declaring type has no containing type to scan.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task DetachedFieldHasNoContainingTypeAsync()
    {
        var field = ParseField("static readonly int A = 1;");

        await Assert.That(PrivateStaticReadonlyField.TryGetNonPartialContainingType(field, out var containingType)).IsFalse();
        await Assert.That(containingType).IsNull();
    }

    /// <summary>Verifies references skip the declarator, other names, literals, and non-expression identifiers.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ReferencesExcludeTheDeclaratorAndUnrelatedTokensAsync()
    {
        var type = (TypeDeclarationSyntax)SyntaxFactory.ParseCompilationUnit(
            """
            class C
            {
                static readonly int A = 1;
                int M(int A) => A + C.A + B + "A".Length;
                string N() => nameof(A);
            }
            """).Members[0];
        var declarator = type.DescendantNodes().OfType<VariableDeclaratorSyntax>().Single();
        var references = new List<IdentifierNameSyntax>();
        foreach (var token in type.DescendantTokens())
        {
            if (PrivateStaticReadonlyField.TryGetReference(in token, "A", declarator.Identifier.SpanStart, out var identifier))
            {
                references.Add(identifier);
            }
        }

        await Assert.That(references.Count).IsEqualTo(ExpectedReferenceCount);
        await Assert.That(PrivateStaticReadonlyField.TryGetReference(declarator.Identifier, "A", declarator.Identifier.SpanStart, out var none)).IsFalse();
        await Assert.That(none).IsNull();
    }

    /// <summary>Verifies a qualified reference is used as the whole access and every other reference as itself.</summary>
    /// <param name="expression">An expression containing one reference to <c>A</c>.</param>
    /// <param name="expectedKind">The syntax kind of the usage node.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("C.A", SyntaxKind.SimpleMemberAccessExpression)]
    [Arguments("A", SyntaxKind.IdentifierName)]
    [Arguments("A.Length", SyntaxKind.IdentifierName)]
    [Arguments("A[0]", SyntaxKind.IdentifierName)]
    public async Task QualifiedReferenceIsUsedAsTheWholeAccessAsync(string expression, SyntaxKind expectedKind)
    {
        var identifier = SyntaxFactory.ParseExpression(expression)
            .DescendantNodesAndSelf()
            .OfType<IdentifierNameSyntax>()
            .Single(static name => name.Identifier.ValueText == "A");

        await Assert.That(PrivateStaticReadonlyField.GetUsage(identifier).Kind()).IsEqualTo(expectedKind);
    }

    /// <summary>Parses one field declaration.</summary>
    /// <param name="declaration">The field declaration text.</param>
    /// <returns>The parsed field.</returns>
    private static FieldDeclarationSyntax ParseField(string declaration) =>
        (FieldDeclarationSyntax)SyntaxFactory.ParseMemberDeclaration(declaration)!;
}
