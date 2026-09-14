// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using RoslynCommon.Analyzers.Tests;
using VerifySpacing = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<StyleSharp.Analyzers.SpacingAnalyzer, StyleSharp.Analyzers.SpacingCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Tests token separation and documentation trivia in parsed and constructed syntax.</summary>
public partial class SpacingAnalyzerUnitTest
{
    /// <summary>Checks a colon needs trailing space in declarations and named arguments.</summary>
    /// <param name="markedSource">The source with the missing space marked.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    [Arguments("class B { } class C {|SST1024::|}B { }")]
    [Arguments("class C { int M(int x) => x; int N() => M(x{|SST1024::|}1); }")]
    public Task ColonRequiresTrailingSpaceAsync(string markedSource) => VerifySpacing.VerifyAnalyzerAsync(markedSource);

    /// <summary>Checks empty collections follow the selected spacing option without forcing padding.</summary>
    /// <param name="option">The collection spacing preference.</param>
    /// <param name="expression">The collection expression and expected diagnostic.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("none", "[ {|SST1011:]|}")]
    [Arguments("space", "[ ]")]
    [Arguments("space", "[]")]
    [Arguments("unknown", "[]")]
    public async Task EmptyCollectionSpacingRespectsTheOptionAsync(string option, string expression)
    {
        var test = new VerifySpacing.Test { TestCode = $"class C {{ int[] M() => {expression}; }}" };
        test.TestState.AnalyzerConfigFiles.Add((EditorConfigPath, $"root = true\n[*.cs]\nstylesharp.collection_expression_spacing = {option}\n"));
        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Checks documentation markers embedded in text are not treated as line exteriors.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task EmbeddedDocumentationSlashesAreCleanAsync() => VerifySpacing.VerifyAnalyzerAsync("""
        /// <summary>Text x///text and ////text.</summary>
        ///
        class C { }
        //
        """);

    /// <summary>Checks missing spaces after operators and semicolons are reported.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task FollowingOperatorAndSemicolonSpacesAreRequiredAsync() => VerifySpacing.VerifyAnalyzerAsync("""
        class C
        {
            void M() { int x =1; x++{|SST1002:;|}int y = 2; x = x {|SST1003:+|}1; }
        }
        """);

    /// <summary>Checks spacing around type parameters, qualified names, and prefix operators.</summary>
    /// <param name="source">The source with unwanted spacing marked.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    [Arguments("class C {|SST1014:<|}T {|SST1015:>|} { }")]
    [Arguments("class C { System {|SST1019:.|}String M(string x) => x?. {|SST1019:ToString|}(); }")]
    [Arguments("class C { void M(int x) { ++ {|SST1020:x|}; x++ {|SST1002:;|} } }")]
    [Arguments("class C { void M(int x) { x {|SST1003:+=|}1; } }")]
    [Arguments("interface I { void M(); } class C : I { void I . M() { } }")]
    public Task AdditionalTokenKindsRespectSpacingAsync(string source) => VerifySpacing.VerifyAnalyzerAsync(source);

    /// <summary>Checks a documentation exterior at the end of the file needs no following text.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task FinalDocumentationExteriorIsCleanAsync() => VerifySpacing.VerifyAnalyzerAsync("class C { }\n///");

    /// <summary>Checks whitespace attached to the following token is recognized in constructed syntax trees.</summary>
    /// <param name="lineBreak">Whether the leading trivia includes a newline.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments(true)]
    [Arguments(false)]
    public async Task LeadingTokenTriviaSeparatesTokensAsync(bool lineBreak)
    {
        var root = SyntaxFactory.ParseCompilationUnit("class C { }");
        var declaration = (ClassDeclarationSyntax)root.Members[0];
        var trivia = SyntaxFactory.TriviaList(SyntaxFactory.Comment("/* keep */"));
        if (lineBreak)
        {
            trivia = trivia.Add(SyntaxFactory.EndOfLine("\n"));
        }

        var changed = declaration.WithKeyword(declaration.Keyword.WithTrailingTrivia(default(SyntaxTriviaList))).WithIdentifier(declaration.Identifier.WithLeadingTrivia(trivia));
        var tree = CSharpSyntaxTree.Create(root.ReplaceNode(declaration, changed));
        var compilation = CSharpCompilation.Create("LeadingTrivia", [tree], RuntimeMetadataReferences.Platform);
        var diagnostics = await compilation.WithAnalyzers([new SpacingAnalyzer()]).GetAnalyzerDiagnosticsAsync();
        await Assert.That(diagnostics).IsEmpty();
    }
}
