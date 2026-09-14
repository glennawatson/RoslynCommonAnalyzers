// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Composition.Hosting;
using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;
using VerifyCapitalFix = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.DocumentationTextAnalyzer,
    StyleSharp.Analyzers.Sst1628TextBeginsWithCapitalCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for the SST1628 code fix (documentation text begins with a capital letter).</summary>
public class Sst1628TextBeginsWithCapitalCodeFixUnitTest
{
    /// <summary>Verifies stale documentation diagnostics neither offer nor apply capitalization.</summary>
    /// <param name="summary">The documentation element, or empty text for a non-documentation target.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    [Arguments("")]
    [Arguments("<summary></summary>")]
    [Arguments("<summary>   </summary>")]
    [Arguments("<summary><c>word</c> follows.</summary>")]
    [Arguments("<summary> \n/// <c>word</c> follows.</summary>")]
    [Arguments("<summary>Already capitalized.</summary>")]
    [Arguments("<summary>123 items.</summary>")]
    public async Task InapplicableSummaryIsUnchangedAsync(string summary)
    {
        using var workspace = new AdhocWorkspace();
        var document = workspace.AddProject("SummaryTarget", LanguageNames.CSharp)
            .AddDocument("Test.cs", $"/// {summary}\nclass C {{ }}");
        var root = (await document.GetSyntaxRootAsync())!;
        var target = root.DescendantNodes(descendIntoTrivia: true).OfType<XmlElementSyntax>().FirstOrDefault();
        var diagnostic = Diagnostic.Create(DocumentationRules.TextBeginsWithCapital, (target ?? (SyntaxNode)root).GetLocation());
        using var container = new ContainerConfiguration().WithPart<Sst1628TextBeginsWithCapitalCodeFixProvider>().CreateContainer();
        var provider = container.GetExport<CodeFixProvider>();
        var actions = new List<CodeAction>();
        await provider.RegisterCodeFixesAsync(new(document, diagnostic, (action, _) => actions.Add(action), CancellationToken.None));
        await Assert.That(actions).IsEmpty();
        var editor = await DocumentEditor.CreateAsync(document);
        ((IBatchFixableCodeFix)provider).RegisterBatchEdits(editor, diagnostic);
        await Assert.That(editor.GetChangedRoot().ToFullString()).IsEqualTo(root.ToFullString());
    }

    /// <summary>Verifies a single-line summary gains its capital.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SingleLineSummaryIsCapitalizedAsync()
    {
        const string Source = """
                              internal class C
                              {
                                  /// {|SST1628:<summary>does a thing.</summary>|}
                                  public void M()
                                  {
                                  }
                              }
                              """;
        const string FixedSource = """
                                   internal class C
                                   {
                                       /// <summary>Does a thing.</summary>
                                       public void M()
                                       {
                                       }
                                   }
                                   """;
        await VerifyCapitalFix.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a summary written across lines gains its capital on the first word.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task WrappedSummaryIsCapitalizedAsync()
    {
        const string Source = """
                              internal class C
                              {
                                  /// {|SST1628:<summary>
                                  /// does a thing worth describing.
                                  /// </summary>|}
                                  public void M()
                                  {
                                  }
                              }
                              """;
        const string FixedSource = """
                                   internal class C
                                   {
                                       /// <summary>
                                       /// Does a thing worth describing.
                                       /// </summary>
                                       public void M()
                                       {
                                       }
                                   }
                                   """;
        await VerifyCapitalFix.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a summary that opens with a code element is left alone.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    /// <remarks>
    /// The first word is a code fragment whose casing the language decides, so there is no
    /// sentence-initial letter to capitalise and changing the one inside would break the fragment.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task SummaryOpeningWithACodeElementIsCleanAsync() =>
        VerifyCapitalFix.VerifyAnalyzerAsync(
            """
            internal class C
            {
                /// <summary><c>while (true)</c> loops forever.</summary>
                public void M()
                {
                }
            }
            """);

    /// <summary>Verifies a summary that opens with a parameter reference is left alone.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task SummaryOpeningWithAParameterReferenceIsCleanAsync() =>
        VerifyCapitalFix.VerifyAnalyzerAsync(
            """
            internal class C
            {
                /// <summary><paramref name="value"/> decides the result.</summary>
                /// <param name="value">The deciding value.</param>
                public void M(int value)
                {
                }
            }
            """);
}
