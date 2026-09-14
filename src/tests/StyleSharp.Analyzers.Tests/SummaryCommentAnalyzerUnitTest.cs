// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Composition.Hosting;
using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Text;
using Verify = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.Sst1663SummaryCommentAnalyzer,
    StyleSharp.Analyzers.Sst1663SummaryCommentCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST1663 (a summary-like comment should be a documentation comment).</summary>
public class SummaryCommentAnalyzerUnitTest
{
    /// <summary>Verifies an undocumented public member without a line comment has nothing to convert.</summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Test]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task MissingCommentIsCleanAsync() =>
        Verify.VerifyAnalyzerAsync("public class C { }");

    /// <summary>Verifies comments without leading prose do not become documentation.</summary>
    /// <param name="comment">The comment immediately before a public declaration.</param>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Test]
    [Arguments("//")]
    [Arguments("//   ")]
    [Arguments("// ----")]
    [Arguments("// 123")]
    [Arguments("//// Keeps extra slashes")]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task NonProseCommentIsCleanAsync(string comment) =>
        Verify.VerifyAnalyzerAsync($$"""
            {{comment}}
            public class C { }
            """);

    /// <summary>Verifies a documented member keeps its existing documentation and extra comment.</summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Test]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task ExistingDocumentationIsCleanAsync() =>
        Verify.VerifyAnalyzerAsync("""
            /// <summary>Existing documentation.</summary>
            // Additional prose
            public class C { }
            """);

    /// <summary>Verifies nonwhitespace trivia between a comment and declaration breaks adjacency.</summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Test]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task InterveningDirectiveIsCleanAsync() =>
        Verify.VerifyAnalyzerAsync("""
            // Describes the class
            #region Members
            public class C { }
            #endregion
            """);

    /// <summary>Verifies a comment sharing its line with another comment is not a standalone summary.</summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Test]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task SharedCommentLineIsCleanAsync() =>
        Verify.VerifyAnalyzerAsync("""
            /* Prefix */ // Describes the class
            public class C { }
            """);

    /// <summary>Verifies a prose comment at the beginning of a file needs no preceding newline.</summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Test]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task FirstLineCommentIsReportedAsync() =>
        Verify.VerifyAnalyzerAsync("""
            {|SST1663://Describes the class|}
            public class C { }
            """);

    /// <summary>Verifies a blank line separates an earlier comment from the member's summary.</summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Test]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task BlankLineAboveSummaryIsReportedAsync() =>
        Verify.VerifyAnalyzerAsync("""
            // Earlier comment

            {|SST1663:// Describes the class|}
            public class C { }
            """);

    /// <summary>Verifies a preceding block comment is not a contiguous double-slash comment block.</summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Test]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task SeparateBlockCommentAboveSummaryIsReportedAsync() =>
        Verify.VerifyAnalyzerAsync("""
            /* Earlier comment */
            {|SST1663:// Describes the class|}
            public class C { }
            """);

    /// <summary>Verifies a comment separated from the member by a blank line is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task BlankLineSeparatedCommentIsCleanAsync() =>
        Verify.VerifyAnalyzerAsync(
            """
            internal class C
            {
                // Not a summary

                public int Count { get; }
            }
            """);

    /// <summary>Verifies a stacked multi-line comment block is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task StackedCommentBlockIsCleanAsync() =>
        Verify.VerifyAnalyzerAsync(
            """
            internal class C
            {
                // First line
                // Second line
                public int Count { get; }
            }
            """);

    /// <summary>Verifies a comment above a non-public member is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task NonPublicMemberIsIgnoredAsync() =>
        Verify.VerifyAnalyzerAsync(
            """
            internal class C
            {
                // Gets the count
                private int Count { get; }
            }
            """);

    /// <summary>Verifies a summary-like comment above a public member is converted to documentation.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SummaryLikeCommentIsConvertedAsync()
    {
        const string Source = """
                              internal class C
                              {
                                  {|SST1663:// Gets the widget count|}
                                  public int Count { get; }
                              }
                              """;
        const string FixedSource = """
                                   internal class C
                                   {
                                       /// <summary>Gets the widget count</summary>
                                       public int Count { get; }
                                   }
                                   """;

        await Verify.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies XML-significant characters in the comment are escaped during conversion.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SpecialCharactersAreEscapedAsync()
    {
        const string Source = """
                              internal class C
                              {
                                  {|SST1663:// Uses a & b < c|}
                                  public int Value { get; }
                              }
                              """;
        const string FixedSource = """
                                   internal class C
                                   {
                                       /// <summary>Uses a &amp; b &lt; c</summary>
                                       public int Value { get; }
                                   }
                                   """;

        await Verify.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies conversion trims the comment and escapes every XML delimiter in single and batch edits.</summary>
    /// <param name="comment">The single-line comment text.</param>
    /// <param name="summary">The expected escaped summary content.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("//  a > b & c < d  ", "a &gt; b &amp; c &lt; d")]
    [Arguments("//   ", "")]
    [Arguments("// Returns \"quoted\" text", "Returns \"quoted\" text")]
    public async Task CommentTextConvertedInSingleAndBatchEditsAsync(string comment, string summary)
    {
        var source = $"class C\n{{\n    {comment}\n    public int Value {{ get; }}\n}}";
        var expected = $"class C\n{{\n    /// <summary>{summary}</summary>\n    public int Value {{ get; }}\n}}";
        using var workspace = new AdhocWorkspace();
        var document = workspace.AddProject(nameof(Test), LanguageNames.CSharp).AddDocument("Summary.cs", source);
        var root = (await document.GetSyntaxRootAsync())!;
        var location = Location.Create(root.SyntaxTree, new(source.IndexOf(comment, StringComparison.Ordinal), comment.Length));
        var diagnostic = Diagnostic.Create(DocumentationRules.SummaryComment, location);
        using var container = new ContainerConfiguration().WithPart<Sst1663SummaryCommentCodeFixProvider>().CreateContainer();
        var provider = container.GetExport<CodeFixProvider>();
        var actions = new List<CodeAction>();
        await provider.RegisterCodeFixesAsync(new(document, diagnostic, (action, _) => actions.Add(action), CancellationToken.None));
        await Assert.That(actions.Count).IsEqualTo(1);
        var operations = await actions[0].GetOperationsAsync(CancellationToken.None);
        var changed = operations.OfType<ApplyChangesOperation>().Single().ChangedSolution.GetDocument(document.Id)!;
        await Assert.That((await changed.GetTextAsync()).ToString()).IsEqualTo(expected);
        var text = await document.GetTextAsync();
        var changes = new List<TextChange>();
        ((ITextChangeBatchableCodeFix)provider).RegisterTextChanges(text, root, diagnostic, changes);
        await Assert.That(changes.Count).IsEqualTo(1);
        await Assert.That(text.WithChanges(changes).ToString()).IsEqualTo(expected);
    }

    /// <summary>Verifies stale diagnostics outside a single-line comment produce no individual or batch edit.</summary>
    /// <param name="source">The document whose initial token or trivia is no longer a convertible comment.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("class C { }")]
    [Arguments("/* Summary */\nclass C { }")]
    [Arguments("/// <summary>Existing documentation.</summary>\nclass C { }")]
    public async Task NonSingleLineCommentHasNoFixAsync(string source)
    {
        using var workspace = new AdhocWorkspace();
        var document = workspace.AddProject(nameof(Test), LanguageNames.CSharp).AddDocument("Summary.cs", source);
        var root = (await document.GetSyntaxRootAsync())!;
        var diagnostic = Diagnostic.Create(DocumentationRules.SummaryComment, Location.Create(root.SyntaxTree, new(0, 1)));
        using var container = new ContainerConfiguration().WithPart<Sst1663SummaryCommentCodeFixProvider>().CreateContainer();
        var provider = container.GetExport<CodeFixProvider>();
        var actions = new List<CodeAction>();
        await provider.RegisterCodeFixesAsync(new(document, diagnostic, (action, _) => actions.Add(action), CancellationToken.None));
        await Assert.That(actions).IsEmpty();
        var text = await document.GetTextAsync();
        var changes = new List<TextChange>();
        ((ITextChangeBatchableCodeFix)provider).RegisterTextChanges(text, root, diagnostic, changes);
        await Assert.That(changes).IsEmpty();
        await Assert.That(provider.FixableDiagnosticIds).Contains("SST1663");
        await Assert.That(provider.GetFixAllProvider()).IsSameReferenceAs(TextChangeBatchFixAllProvider.Instance);
    }
}
