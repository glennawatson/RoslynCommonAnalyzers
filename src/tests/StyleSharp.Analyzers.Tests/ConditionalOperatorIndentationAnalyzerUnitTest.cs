// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Composition.Hosting;
using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Text;
using VerifyConditionalOperatorIndentation = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.Sst1145ConditionalOperatorPlacementAnalyzer,
    StyleSharp.Analyzers.Sst1140ConditionalOperatorIndentationCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST1140 (start wrapped conditional operators on indented continuation lines).</summary>
public class ConditionalOperatorIndentationAnalyzerUnitTest
{
    /// <summary>Verifies stale locations and comments prevent registration, direct application, and batch edits.</summary>
    /// <param name="expression">The conditional or replacement expression.</param>
    /// <param name="target">The token carrying the diagnostic.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("1", "1")]
    [Arguments("c /* keep */ ? 1 : 2", "?")]
    [Arguments("c ? /* keep */ 1 : 2", "?")]
    [Arguments("c ? 1 /* keep */ : 2", ":")]
    [Arguments("c ? 1 : /* keep */ 2", ":")]
    public async Task UnsafeOrStaleConditionalIsUnchangedAsync(string expression, string target)
    {
        using var workspace = new AdhocWorkspace();
        var document = workspace.AddProject("Conditional", LanguageNames.CSharp)
            .AddDocument("Test.cs", $"class C {{ int M(bool c) => {expression}; }}");
        var root = (await document.GetSyntaxRootAsync())!;
        var text = await document.GetTextAsync();
        var token = root.DescendantTokens().First(token => token.Text == target);
        var diagnostic = Diagnostic.Create(ReadabilityRules.ConditionalOperatorIndentedLine, token.GetLocation());
        using var container = new ContainerConfiguration().WithPart<Sst1140ConditionalOperatorIndentationCodeFixProvider>().CreateContainer();
        var provider = container.GetExport<CodeFixProvider>();
        var actions = new List<CodeAction>();
        await provider.RegisterCodeFixesAsync(new(document, diagnostic, (action, _) => actions.Add(action), CancellationToken.None));
        await Assert.That(actions).IsEmpty();
        var applied = await TextChangeCodeFix.ApplyAsync(document, diagnostic, Sst1140ConditionalOperatorIndentationCodeFixProvider.RegisterTextChanges, CancellationToken.None);
        await Assert.That(applied).IsSameReferenceAs(document);
        var changes = new List<TextChange>();
        Sst1140ConditionalOperatorIndentationCodeFixProvider.RegisterTextChanges(text, root, diagnostic, changes);
        await Assert.That(changes).IsEmpty();
    }

    /// <summary>Verifies trailing conditional operators are moved to indented branch-leading lines.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task TrailingOperatorsAreReflowedAsync()
    {
        const string Source = """
                              public class C
                              {
                                  public int M(bool c) => c {|SST1140:?|}
                                      1 {|SST1140::|}
                                      2;
                              }
                              """;
        const string FixedSource = """
                                   public class C
                                   {
                                       public int M(bool c) => c
                                           ? 1
                                           : 2;
                                   }
                                   """;

        await VerifyConditionalOperatorIndentation.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies leading conditional operators at the wrong indentation are reindented.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task LeadingOperatorsAtWrongIndentAreReflowedAsync()
    {
        const string Source = """
                              public class C
                              {
                                  public int M(bool c) => c
                              {|SST1140:?|} 1
                              {|SST1140::|} 2;
                              }
                              """;
        const string FixedSource = """
                                   public class C
                                   {
                                       public int M(bool c) => c
                                           ? 1
                                           : 2;
                                   }
                                   """;

        await VerifyConditionalOperatorIndentation.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies correctly indented conditional operators are clean when branch expressions wrap later.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task WrappedBranchesAfterLeadingOperatorsAreCleanAsync() =>
        VerifyConditionalOperatorIndentation.VerifyAnalyzerAsync(
            """
            public class C
            {
                public int M(bool c) => c
                    ? Build(
                        1,
                        2)
                    : Build(
                        3,
                        4);

                private int Build(int x, int y) => x + y;
            }
            """);

    /// <summary>Verifies leading operators are clean when the condition starts after a wrapped signature.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task WrappedExpressionBodiedMemberSignatureIsCleanAsync() =>
        VerifyConditionalOperatorIndentation.VerifyAnalyzerAsync(
            """
            public class C
            {
                public int M(
                    bool c,
                    int whenTrue,
                    int whenFalse) =>
                    c
                        ? whenTrue
                        : whenFalse;
            }
            """);

    /// <summary>Verifies single-line conditionals are not reported.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task SingleLineConditionalIsCleanAsync() =>
        VerifyConditionalOperatorIndentation.VerifyAnalyzerAsync(
            """
            public class C
            {
                public int M(bool c) => c ? 1 : 2;
            }
            """);
}
