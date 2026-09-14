// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Composition.Hosting;
using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Editing;
using RoslynCommon.Analyzers.Tests;
using VerifyBuiltInAlias = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.Sst1121BuiltInTypeAliasAnalyzer,
    StyleSharp.Analyzers.Sst1121BuiltInTypeAliasCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for the built-in-type-alias rule (SST1121, opt-in).</summary>
public class BuiltInTypeAliasAnalyzerUnitTest
{
    /// <summary>Verifies registration and batch editing bind the diagnostic again and preserve surrounding trivia.</summary>
    /// <param name="source">The document containing the selected node.</param>
    /// <param name="target">The selected diagnostic text.</param>
    /// <param name="expected">The changed document, or null when the diagnostic is stale.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("using S = System; class C { S::Int32 value; }", "S::Int32", "using S = System; class C { int value; }")]
    [Arguments("class C { int M() => System.Int32.MaxValue; }", "System.Int32", "class C { int M() => int.MaxValue; }")]
    [Arguments("class C { /* before */ System.Int32 /* after */ value; }", "System.Int32", "class C { /* before */ int /* after */ value; }")]
    [Arguments("class C { System.DateTime value; }", "System.DateTime", null)]
    [Arguments("using S = System; class C { S::DateTime value; }", "S::DateTime", null)]
    [Arguments("class C { object M() => System.DateTime.Now; }", "System.DateTime", null)]
    [Arguments("class C { object M() => System.Int32.MaxValue; }", "System.Int32.MaxValue", null)]
    [Arguments("class C { Int32 value; } class Int32 { }", "Int32", null)]
    [Arguments("class C { Fake.Int32 value; } namespace Fake { class Int32 { } }", "Fake.Int32", null)]
    [Arguments("class C { Missing value; }", "Missing", null)]
    [Arguments("class C { int M() => 1; }", "1", null)]
    public async Task DiagnosticMustStillNameAnAliasedFrameworkTypeAsync(string source, string target, string? expected)
    {
        using var workspace = new AdhocWorkspace();
        var document = workspace.AddProject("BuiltInAlias", LanguageNames.CSharp)
            .WithMetadataReferences(RuntimeMetadataReferences.Platform).AddDocument("Test.cs", source);
        var root = (await document.GetSyntaxRootAsync())!;
        var diagnostic = LanguageStyleCodeFixProviderTests.CreateDiagnostic(root, "SST1121", target);
        using var container = new ContainerConfiguration().WithPart<Sst1121BuiltInTypeAliasCodeFixProvider>().CreateContainer();
        var provider = container.GetExport<CodeFixProvider>();
        var actions = new List<CodeAction>();
        await provider.RegisterCodeFixesAsync(new(document, diagnostic, (action, _) => actions.Add(action), CancellationToken.None));
        await Assert.That(actions.Count).IsEqualTo(expected is null ? 0 : 1);
        if (expected is not null)
        {
            var operations = await actions[0].GetOperationsAsync(CancellationToken.None);
            var changed = operations.OfType<ApplyChangesOperation>().Single().ChangedSolution.GetDocument(document.Id)!;
            await Assert.That((await changed.GetTextAsync()).ToString()).IsEqualTo(expected);
        }

        var editor = await DocumentEditor.CreateAsync(document);
        await Sst1121BuiltInTypeAliasCodeFixProvider.RegisterEditsAsync(editor, diagnostic, CancellationToken.None);
        await Assert.That(editor.GetChangedRoot().ToFullString()).IsEqualTo(expected ?? source);
    }

    /// <summary>Verifies a qualified framework type name is reported (SST1121) and replaced with its alias.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task QualifiedFrameworkNameReplacedAsync()
    {
        const string Source = """
                              internal class C
                              {
                                  private {|SST1121:System.Int32|} value;
                              }
                              """;
        const string FixedSource = """
                                   internal class C
                                   {
                                       private int value;
                                   }
                                   """;
        await VerifyBuiltInAlias.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a bare framework type name is reported (SST1121) and replaced with its alias.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task BareFrameworkNameReplacedAsync()
    {
        const string Source = """
                              using System;

                              internal class C
                              {
                                  private {|SST1121:Int32|} value;
                              }
                              """;
        const string FixedSource = """
                                   using System;

                                   internal class C
                                   {
                                       private int value;
                                   }
                                   """;
        await VerifyBuiltInAlias.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies the keyword alias is not flagged.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task KeywordAliasIsCleanAsync() =>
        VerifyBuiltInAlias.VerifyAnalyzerAsync(
            """
            internal class C
            {
                private int value;
            }
            """);

    /// <summary>Verifies a framework name a <c>nameof</c> takes the name of is left alone.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    /// <remarks><c>nameof</c> takes a name and a keyword is not one, so <c>nameof(object)</c> does not compile.</remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task NameofOperandIsCleanAsync() =>
        VerifyBuiltInAlias.VerifyAnalyzerAsync(
            """
            using System;

            internal class C
            {
                private string Name() => nameof(Object);
            }
            """);
}
