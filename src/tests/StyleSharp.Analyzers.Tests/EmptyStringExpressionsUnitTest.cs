// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

using RoslynCommon.Analyzers.Tests;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Tests the shared recognition of the empty string written as <c>""</c> or <c>string.Empty</c>.</summary>
public sealed class EmptyStringExpressionsUnitTest
{
    /// <summary>The source whose array elements the tests inspect, in the order the index constants name them.</summary>
    private const string Source =
        """
        using System;

        class Other
        {
            public static string Empty = "x";
        }

        class Probe
        {
            static string Empty = "";

            object[] Values() => new object[] { "", "a", @"", string.Empty, String.Empty, Other.Empty, 0, null, Empty };
        }
        """;

    /// <summary>The literal <c>""</c>.</summary>
    private const int EmptyLiteral = 0;

    /// <summary>The literal <c>"a"</c>.</summary>
    private const int NonEmptyLiteral = 1;

    /// <summary>The verbatim literal <c>@""</c>.</summary>
    private const int EmptyVerbatimLiteral = 2;

    /// <summary>The keyword-qualified <c>string.Empty</c>.</summary>
    private const int KeywordStringEmpty = 3;

    /// <summary>The type-name-qualified <c>String.Empty</c>.</summary>
    private const int TypeNameStringEmpty = 4;

    /// <summary>A static <c>Empty</c> field on another type.</summary>
    private const int OtherTypeEmpty = 5;

    /// <summary>The numeric literal <c>0</c>.</summary>
    private const int NumericLiteral = 6;

    /// <summary>The <see langword="null"/> literal.</summary>
    private const int NullLiteral = 7;

    /// <summary>A bare identifier named <c>Empty</c>.</summary>
    private const int BareEmpty = 8;

    /// <summary>The parsed source.</summary>
    private static readonly SyntaxTree Tree = CSharpSyntaxTree.ParseText(Source);

    /// <summary>The semantic model the field checks bind against.</summary>
    private static readonly SemanticModel Model = CSharpCompilation.Create(
        nameof(EmptyStringExpressionsUnitTest),
        [Tree],
        RuntimeMetadataReferences.Platform).GetSemanticModel(Tree);

    /// <summary>The array elements under test.</summary>
    private static readonly SeparatedSyntaxList<ExpressionSyntax> Values = FindValues();

    /// <summary>Verifies only a string literal with no characters is the empty literal.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task IsEmptyStringLiteralAcceptsOnlyEmptyStringLiteralsAsync()
    {
        await Assert.That(EmptyStringExpressions.IsEmptyStringLiteral(Values[EmptyLiteral])).IsTrue();
        await Assert.That(EmptyStringExpressions.IsEmptyStringLiteral(Values[EmptyVerbatimLiteral])).IsTrue();
        await Assert.That(EmptyStringExpressions.IsEmptyStringLiteral(Values[NonEmptyLiteral])).IsFalse();
        await Assert.That(EmptyStringExpressions.IsEmptyStringLiteral(Values[KeywordStringEmpty])).IsFalse();
        await Assert.That(EmptyStringExpressions.IsEmptyStringLiteral(Values[NumericLiteral])).IsFalse();
        await Assert.That(EmptyStringExpressions.IsEmptyStringLiteral(Values[NullLiteral])).IsFalse();
    }

    /// <summary>Verifies any qualified <c>.Empty</c> access matches syntactically and a bare identifier does not.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task IsEmptyMemberAccessMatchesQualifiedEmptyAccessesAsync()
    {
        await Assert.That(EmptyStringExpressions.IsEmptyMemberAccess(Values[KeywordStringEmpty])).IsTrue();
        await Assert.That(EmptyStringExpressions.IsEmptyMemberAccess(Values[TypeNameStringEmpty])).IsTrue();
        await Assert.That(EmptyStringExpressions.IsEmptyMemberAccess(Values[OtherTypeEmpty])).IsTrue();
        await Assert.That(EmptyStringExpressions.IsEmptyMemberAccess(Values[BareEmpty])).IsFalse();
        await Assert.That(EmptyStringExpressions.IsEmptyMemberAccess(Values[EmptyLiteral])).IsFalse();
    }

    /// <summary>Verifies only an access binding to the field on <see cref="string"/> is <see cref="string.Empty"/>.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task IsStringEmptyFieldRequiresTheStringFieldAsync()
    {
        await Assert.That(EmptyStringExpressions.IsStringEmptyField(Model, Values[KeywordStringEmpty], CancellationToken.None)).IsTrue();
        await Assert.That(EmptyStringExpressions.IsStringEmptyField(Model, Values[TypeNameStringEmpty], CancellationToken.None)).IsTrue();
        await Assert.That(EmptyStringExpressions.IsStringEmptyField(Model, Values[OtherTypeEmpty], CancellationToken.None)).IsFalse();
        await Assert.That(EmptyStringExpressions.IsStringEmptyField(Model, Values[BareEmpty], CancellationToken.None)).IsFalse();
        await Assert.That(EmptyStringExpressions.IsStringEmptyField(Model, Values[EmptyLiteral], CancellationToken.None)).IsFalse();
    }

    /// <summary>Finds the array initializer's elements.</summary>
    /// <returns>The elements in source order.</returns>
    /// <exception cref="InvalidOperationException">The source has no array initializer.</exception>
    private static SeparatedSyntaxList<ExpressionSyntax> FindValues()
    {
        foreach (var node in Tree.GetRoot().DescendantNodes())
        {
            if (node is InitializerExpressionSyntax initializer)
            {
                return initializer.Expressions;
            }
        }

        throw new InvalidOperationException("The source has no array initializer.");
    }
}
