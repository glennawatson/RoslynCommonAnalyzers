// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Tests whether moving members crosses directive boundaries.</summary>
public sealed class DirectiveBoundariesTests
{
    /// <summary>Verifies paired regions may be crossed only when the entire region is in the gap.</summary>
    /// <param name="before">Directives preceding both members.</param>
    /// <param name="between">Directives between the members.</param>
    /// <param name="after">Directives following both members.</param>
    /// <param name="separate">Whether any directive separates the members.</param>
    /// <param name="unbalanced">Whether moving across the gap changes directive scope.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("", "", "", false, false)]
    [Arguments("", "#region R\n#endregion\n", "", true, false)]
    [Arguments("", "#if true\n#endif\n", "", true, false)]
    [Arguments("", "#if false\n#elif true\n#else\n#endif\n", "", true, false)]
    [Arguments("", "#region R\n", "#endregion\n", true, true)]
    [Arguments("#region R\n", "#endregion\n", "", true, true)]
    [Arguments("", "#nullable enable\n", "", true, true)]
    [Arguments("#nullable enable\n", "", "", false, false)]
    [Arguments("", "", "#nullable enable\n", false, false)]
    public async Task MemberGapHonorsDirectiveScopeAsync(string before, string between, string after, bool separate, bool unbalanced)
    {
        var root = await CSharpSyntaxTree.ParseText($"class C {{\n{before}int first;\n{between}int second;\n{after}}}").GetRootAsync();
        var fields = root.DescendantNodes().OfType<FieldDeclarationSyntax>().ToArray();
        await Assert.That(DirectiveBoundaries.Separate(fields[0], fields[1])).IsEqualTo(separate);
        await Assert.That(DirectiveBoundaries.Separate(fields[1], fields[0])).IsEqualTo(separate);
        await Assert.That(DirectiveBoundaries.SeparateUnbalanced(fields[0], fields[1])).IsEqualTo(unbalanced);
        await Assert.That(DirectiveBoundaries.SeparateUnbalanced(fields[1], fields[0])).IsEqualTo(unbalanced);
    }

    /// <summary>Verifies detached or overlapping nodes cannot enclose a gap.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task DetachedAndOverlappingNodesHaveNoGapAsync()
    {
        var detached = SyntaxFactory.ClassDeclaration("C");
        var root = await CSharpSyntaxTree.ParseText("class C { int value; }").GetRootAsync();
        var type = root.DescendantNodes().OfType<ClassDeclarationSyntax>().Single();
        var field = type.Members.Single();
        await Assert.That(DirectiveBoundaries.Separate(detached, type)).IsFalse();
        await Assert.That(DirectiveBoundaries.SeparateUnbalanced(detached, type)).IsFalse();
        await Assert.That(DirectiveBoundaries.Separate(type, field)).IsFalse();
        await Assert.That(DirectiveBoundaries.SeparateUnbalanced(type, field)).IsFalse();
        await Assert.That(DirectiveBoundaries.SeparateMembers(detached)).IsFalse();
        var record = (await CSharpSyntaxTree.ParseText("record R;").GetRootAsync()).DescendantNodes().OfType<RecordDeclarationSyntax>().Single();
        await Assert.That(DirectiveBoundaries.SeparateMembers(record)).IsFalse();
    }

    /// <summary>Verifies only directives whose start lies inside the half-open span count.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task SpanEndpointsAndLeadingDirectivesAreRespectedAsync()
    {
        var root = await CSharpSyntaxTree.ParseText("#nullable enable\nclass C { int value; }").GetRootAsync();
        var directive = root.GetFirstDirective()!;
        var type = root.DescendantNodes().OfType<ClassDeclarationSyntax>().Single();
        await Assert.That(DirectiveBoundaries.Cross(root, new(directive.SpanStart, 1))).IsTrue();
        await Assert.That(DirectiveBoundaries.Cross(root, new(directive.SpanStart, 0))).IsFalse();
        await Assert.That(DirectiveBoundaries.Cross(root, type.Span)).IsFalse();
        await Assert.That(DirectiveBoundaries.SeparateMembers(type)).IsFalse();
    }

    /// <summary>Verifies conditional detection distinguishes conditional families from other directives.</summary>
    /// <param name="source">The source containing the directives.</param>
    /// <param name="expected">Whether a conditional directive occurs.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("class C { }", false)]
    [Arguments("#nullable enable\nclass C { }", false)]
    [Arguments("#region R\nclass C { }\n#endregion", false)]
    [Arguments("#if true\nclass C { }\n#endif", true)]
    [Arguments("#elif true\n", false)]
    [Arguments("#else\n", false)]
    [Arguments("#endif\n", false)]
    public async Task ConditionalFamiliesAreRecognizedAsync(string source, bool expected)
    {
        var root = await CSharpSyntaxTree.ParseText(source).GetRootAsync();
        await Assert.That(DirectiveBoundaries.AnyConditional(root)).IsEqualTo(expected);
    }

    /// <summary>Verifies a subtree containing only a later directive in a valid conditional region is recognized.</summary>
    /// <param name="kind">The conditional directive retained in the subtree.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments(SyntaxKind.ElifDirectiveTrivia)]
    [Arguments(SyntaxKind.ElseDirectiveTrivia)]
    [Arguments(SyntaxKind.EndIfDirectiveTrivia)]
    public async Task LaterConditionalDirectiveIsRecognizedAsync(SyntaxKind kind)
    {
        var root = await CSharpSyntaxTree.ParseText("#if true\n#elif false\n#else\n#endif\n").GetRootAsync();
        var trivia = root.DescendantTrivia().Single(candidate => candidate.IsKind(kind));
        var subtree = SyntaxFactory.CompilationUnit().WithLeadingTrivia(trivia);
        await Assert.That(DirectiveBoundaries.AnyConditional(subtree)).IsTrue();
    }
}
