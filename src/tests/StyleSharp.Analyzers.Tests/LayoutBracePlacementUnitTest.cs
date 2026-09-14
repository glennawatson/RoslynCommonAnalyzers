// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Composition.Hosting;
using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Text;
using VerifyBlank = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.BracePlacementAnalyzer,
    StyleSharp.Analyzers.BlankLineRemovalCodeFixProvider>;
using VerifyBrace = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.BracePlacementAnalyzer,
    StyleSharp.Analyzers.BracePlacementCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for the brace-placement rules (SST1500/SST1505/SST1508/SST1509).</summary>
public class LayoutBracePlacementUnitTest
{
    /// <summary>The source filename used by direct code-fix tests.</summary>
    private const string TestFileName = "Test.cs";

    /// <summary>Verifies direct and batch edits preserve comments and handle both sides of a brace.</summary>
    /// <param name="source">The source receiving a brace diagnostic.</param>
    /// <param name="target">The brace token's text.</param>
    /// <param name="expected">The exact expected source after editing.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("class C { int value;\n}", "{", "class C\n{\n    int value;\n}")]
    [Arguments("class C\r\n{ int value;\r\n}", "{", "class C\r\n{\r\n    int value;\r\n}")]
    [Arguments("class C\n{\n    int value; }", "}", "class C\n{\n    int value;\n}")]
    [Arguments("class C\n{\n}", "{", "class C\n{\n}")]
    [Arguments("class C\n{\n}", "}", "class C\n{\n}")]
    [Arguments("class C /* keep */ {\n}", "{", "class C /* keep */ {\n}")]
    [Arguments("class C\n{ /* keep */ int value;\n}", "{", "class C\n{ /* keep */ int value;\n}")]
    [Arguments("class C\n{ int value; /* keep */ }", "}", "class C\n{ int value; /* keep */ }")]
    [Arguments("{\n}", "{", "{\n}")]
    [Arguments("{", "{", "{")]
    [Arguments("class C {", "{", "class C\n{")]
    [Arguments("class C\n{\n    bool M(object value) => value is { Value: 1 };\n}", "}", "class C\n{\n    bool M(object value) => value is { Value: 1\n    };\n}")]
    public async Task BraceEditsRespectTokenNeighborsAsync(string source, string target, string expected)
    {
        using var workspace = new AdhocWorkspace();
        var document = workspace.AddProject("BraceNeighbors", LanguageNames.CSharp).AddDocument(TestFileName, source);
        var root = (await document.GetSyntaxRootAsync())!;
        var brace = root.FindToken(source.IndexOf(target, StringComparison.Ordinal));
        var diagnostic = Diagnostic.Create(LayoutRules.BracesOnOwnLine, brace.GetLocation());
        using var container = new ContainerConfiguration().WithPart<BracePlacementCodeFixProvider>().CreateContainer();
        var provider = container.GetExport<CodeFixProvider>();
        var actions = new List<CodeAction>();
        await provider.RegisterCodeFixesAsync(new(document, diagnostic, (action, _) => actions.Add(action), CancellationToken.None));
        await Assert.That(actions.Count).IsEqualTo(1);
        var operations = await actions[0].GetOperationsAsync(CancellationToken.None);
        var changed = operations.OfType<ApplyChangesOperation>().Single().ChangedSolution.GetDocument(document.Id)!;
        await Assert.That((await changed.GetTextAsync()).ToString()).IsEqualTo(expected);
        var changes = new List<TextChange>();
        var text = await document.GetTextAsync();
        ((ITextChangeBatchableCodeFix)provider).RegisterTextChanges(text, root, diagnostic, changes);
        await Assert.That(text.WithChanges(changes).ToString()).IsEqualTo(expected);
    }

    /// <summary>Verifies a stale diagnostic on a non-brace token registers no action or batch change.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task NonBraceDiagnosticIsIgnoredAsync()
    {
        using var workspace = new AdhocWorkspace();
        var document = workspace.AddProject("StaleBrace", LanguageNames.CSharp).AddDocument(TestFileName, "class C {}");
        var root = (await document.GetSyntaxRootAsync())!;
        var diagnostic = Diagnostic.Create(LayoutRules.BracesOnOwnLine, root.GetFirstToken().GetLocation());
        using var container = new ContainerConfiguration().WithPart<BracePlacementCodeFixProvider>().CreateContainer();
        var provider = container.GetExport<CodeFixProvider>();
        var actions = new List<CodeAction>();
        await provider.RegisterCodeFixesAsync(new(document, diagnostic, (action, _) => actions.Add(action), CancellationToken.None));
        var changes = new List<TextChange>();
        ((ITextChangeBatchableCodeFix)provider).RegisterTextChanges(await document.GetTextAsync(), root, diagnostic, changes);
        await Assert.That(actions).IsEmpty();
        await Assert.That(changes).IsEmpty();
    }

    /// <summary>Verifies direct and batched fixes remove complete blank runs and preserve stale locations.</summary>
    /// <param name="source">The brace and its surrounding physical lines.</param>
    /// <param name="after">Whether blank lines follow the opening brace.</param>
    /// <param name="expected">The expected remaining text.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("{", true, "{")]
    [Arguments("{\ncode", true, "{\ncode")]
    [Arguments("}", false, "}")]
    [Arguments("code\n}", false, "code\n}")]
    [Arguments("{\n\n \t\ncode", true, "{\ncode")]
    [Arguments("{\n\n \t", true, "{\n")]
    [Arguments("code\n\n \t\n}", false, "code\n}")]
    [Arguments("\n \t\n}", false, "}")]
    [Arguments("{\r\n\r\n \t\r\ncode", true, "{\r\ncode")]
    public async Task BlankLineRunsAndStaleLocationsAreHandledAsync(string source, bool after, string expected)
    {
        using var workspace = new AdhocWorkspace();
        var document = workspace.AddProject("Braces", LanguageNames.CSharp).AddDocument(TestFileName, source);
        var text = await document.GetTextAsync();
        var root = (await document.GetSyntaxRootAsync())!;
        var span = new TextSpan(source.IndexOf(after ? '{' : '}'), 1);
        var descriptor = after ? LayoutRules.OpenBraceNotFollowedByBlankLine : LayoutRules.CloseBraceNotPrecededByBlankLine;
        var diagnostic = Diagnostic.Create(descriptor, Location.Create(root.SyntaxTree, span));
        var changes = new List<TextChange>();
        using var container = new ContainerConfiguration().WithPart<BlankLineRemovalCodeFixProvider>().CreateContainer();
        ((ITextChangeBatchableCodeFix)container.GetExport<CodeFixProvider>()).RegisterTextChanges(text, root, diagnostic, changes);
        await Assert.That(text.WithChanges(changes).ToString()).IsEqualTo(expected);
        await Assert.That(changes.Count).IsEqualTo(source == expected ? 0 : 1);

        var updated = await BlankLineRemovalCodeFixProvider.RemoveBlankLinesAsync(document, span, after, CancellationToken.None);
        await Assert.That((await updated.GetTextAsync()).ToString()).IsEqualTo(expected);
        await Assert.That(ReferenceEquals(updated, document)).IsEqualTo(source == expected);
    }

    /// <summary>Verifies a well-formatted Allman block produces no diagnostics.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task AllmanBlockIsCleanAsync() =>
        VerifyBrace.VerifyAnalyzerAsync(
            """
            internal class C
            {
                private void M()
                {
                    return;
                }
            }
            """);

    /// <summary>Verifies a single-line auto-property accessor list is not flagged.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task SingleLineAccessorListIsCleanAsync() =>
        VerifyBrace.VerifyAnalyzerAsync(
            """
            internal class C
            {
                public int X { get; set; }
            }
            """);

    /// <summary>Verifies a property with an initializer below a blank line is not flagged (the '{' is mid-line).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task PropertyInitializerBelowBlankLineIsCleanAsync() =>
        VerifyBrace.VerifyAnalyzerAsync(
            """
            internal class C
            {
                private int _a;

                public int X { get; set; } = 0;
            }
            """);

    /// <summary>Verifies a brace sharing its line with earlier code is reported (SST1500) and moved to its own line.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SharedLineBraceMovedAsync()
    {
        const string Source = """
            internal class C
            {
                private void M() {|SST1500:{|}
                    return;
                }
            }
            """;
        const string FixedSource = """
            internal class C
            {
                private void M()
                {
                    return;
                }
            }
            """;
        await VerifyBrace.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies Fix All rewrites every shared-line brace (SST1500) in one pass.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task FixAllRewritesEveryOccurrenceAsync()
    {
        const string Source = """
            internal class C
            {
                private void M() {|SST1500:{|}
                    return;
                }

                private void N() {|SST1500:{|}
                    return;
                }

                private void O() {|SST1500:{|}
                    return;
                }
            }
            """;
        const string FixedSource = """
            internal class C
            {
                private void M()
                {
                    return;
                }

                private void N()
                {
                    return;
                }

                private void O()
                {
                    return;
                }
            }
            """;
        await VerifyBrace.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a blank line before an opening brace is reported (SST1509) and removed.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task BlankLineBeforeOpenBraceRemovedAsync()
    {
        const string Source = """
            internal class C
            {
                private void M()

                {|SST1509:{|}
                    return;
                }
            }
            """;
        const string FixedSource = """
            internal class C
            {
                private void M()
                {
                    return;
                }
            }
            """;
        await VerifyBlank.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a blank line after an opening brace is reported (SST1505) and removed.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task BlankLineAfterOpenBraceRemovedAsync()
    {
        const string Source = """
            internal class C
            {
                private void M()
                {|SST1505:{|}

                    return;
                }
            }
            """;
        const string FixedSource = """
            internal class C
            {
                private void M()
                {
                    return;
                }
            }
            """;
        await VerifyBlank.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a blank line before a closing brace is reported (SST1508) and removed.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task BlankLineBeforeCloseBraceRemovedAsync()
    {
        const string Source = """
            internal class C
            {
                private void M()
                {
                    return;

                {|SST1508:}|}
            }
            """;
        const string FixedSource = """
            internal class C
            {
                private void M()
                {
                    return;
                }
            }
            """;
        await VerifyBlank.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies Fix All removes every reported brace-adjacent blank line in one pass (SST1505).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task FixAllRemovesEveryBlankLineOccurrenceAsync()
    {
        const string Source = """
            internal class C
            {
                private void A()
                {|SST1505:{|}

                    return;
                }

                private void B()
                {|SST1505:{|}

                    return;
                }

                private void D()
                {|SST1505:{|}

                    return;
                }
            }
            """;
        const string FixedSource = """
            internal class C
            {
                private void A()
                {
                    return;
                }

                private void B()
                {
                    return;
                }

                private void D()
                {
                    return;
                }
            }
            """;
        await VerifyBlank.VerifyCodeFixAsync(Source, FixedSource);
    }
}
