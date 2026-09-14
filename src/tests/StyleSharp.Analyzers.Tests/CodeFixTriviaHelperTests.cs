// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Tests the trivia transformations shared by the property code fixes.</summary>
public class CodeFixTriviaHelperTests
{
    /// <summary>The replacement property the tests swap in.</summary>
    private const string AutoProperty = "public int Value { get; }\n";

    /// <summary>Verifies removing a field declared above the property closes the gap it leaves.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task FieldAbovePropertyLeavesOneLayoutAsync()
    {
        const string Source = "class C\n{\n    private int _value;\n\n    public int Value { get { return _value; } }\n}\n";

        var updated = Replace(Source);

        await Assert.That(updated).IsEqualTo("class C\n{\n    public int Value { get; }\n}\n");
    }

    /// <summary>Verifies removing a field declared below the property keeps the property's own layout.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task FieldBelowPropertyLeavesPropertyLayoutAsync()
    {
        const string Source = "class C\n{\n    public int Value => _value;\n\n    private int _value;\n}\n";

        var updated = Replace(Source);

        await Assert.That(updated).IsEqualTo("class C\n{\n    public int Value { get; }\n}\n");
    }

    /// <summary>Verifies only one blank line is dropped from the combined leading trivia.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task CollapseLeadingBlankLineDropsOnlyTheFirstBreakAsync()
    {
        var trivia = SyntaxFactory.ParseLeadingTrivia("\n\n\n    ");

        var collapsed = CodeFixTriviaHelper.CollapseLeadingBlankLine(trivia);

        await Assert.That(collapsed.ToFullString()).IsEqualTo("\n\n    ");
    }

    /// <summary>Verifies the indentation is the whitespace immediately before the declaration.</summary>
    /// <param name="leading">The declaration's leading trivia text.</param>
    /// <param name="expected">The indentation text.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("\n    ", "    ")]
    [Arguments("// comment\n\t", "\t")]
    [Arguments("// comment\n", "")]
    [Arguments("", "")]
    public async Task IndentTriviaIsTheTrailingWhitespaceAsync(string leading, string expected)
    {
        var indent = CodeFixTriviaHelper.IndentTrivia(SyntaxFactory.ParseLeadingTrivia(leading));

        await Assert.That(indent.ToFullString()).IsEqualTo(expected);
    }

    /// <summary>Parses a class, rewrites its property to an auto-property and removes its field.</summary>
    /// <param name="source">The class source holding one field and one property.</param>
    /// <returns>The rewritten source text.</returns>
    private static string Replace(string source)
    {
        var root = SyntaxFactory.ParseCompilationUnit(source);
        var property = root.DescendantNodes().OfType<PropertyDeclarationSyntax>().Single();
        var field = root.DescendantNodes().OfType<FieldDeclarationSyntax>().Single();
        var updated = ((PropertyDeclarationSyntax)SyntaxFactory.ParseMemberDeclaration(AutoProperty)!).WithLeadingTrivia(property.GetLeadingTrivia());

        return CodeFixTriviaHelper.ReplacePropertyRemovingField(root, property, updated, field).ToFullString();
    }
}
