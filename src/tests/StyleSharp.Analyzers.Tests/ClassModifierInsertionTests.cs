// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Tests inserting a modifier into a class declaration ahead of <c>partial</c>.</summary>
public class ClassModifierInsertionTests
{
    /// <summary>Verifies where the modifier lands and which token keeps the declaration's leading trivia.</summary>
    /// <param name="source">The class declaration source.</param>
    /// <param name="takePartialIndentation">Whether the modifier takes over the leading trivia of <c>partial</c>.</param>
    /// <param name="expected">The source after insertion.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("// c\nclass C { }", true, "// c\nstatic class C { }")]
    [Arguments("// c\nclass C { }", false, "// c\nstatic class C { }")]
    [Arguments("public class C { }", true, "public static class C { }")]
    [Arguments("public class C { }", false, "public static class C { }")]
    [Arguments("public partial class C { }", true, "public static partial class C { }")]
    [Arguments("public partial class C { }", false, "public static partial class C { }")]
    [Arguments("// c\npartial class C { }", true, "// c\nstatic partial class C { }")]
    [Arguments("// c\npartial class C { }", false, "static // c\npartial class C { }")]
    public async Task InsertsBeforePartialAsync(string source, bool takePartialIndentation, string expected)
    {
        var declaration = SyntaxFactory.ParseCompilationUnit(source).DescendantNodes().OfType<ClassDeclarationSyntax>().Single();

        var updated = ClassModifierInsertion.InsertBeforePartial(declaration, SyntaxKind.StaticKeyword, takePartialIndentation);

        await Assert.That(updated.ToFullString()).IsEqualTo(expected);
    }
}
