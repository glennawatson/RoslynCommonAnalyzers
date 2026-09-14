// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Composition.Hosting;
using System.Runtime.CompilerServices;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.CodeAnalysis.Text;

using RoslynCommon.Analyzers.Tests;

using Verify = PerformanceSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    PerformanceSharp.Analyzers.Psh1014ReadonlyStructAnalyzer,
    PerformanceSharp.Analyzers.Psh1014ReadonlyStructCodeFixProvider>;

namespace PerformanceSharp.Analyzers.Tests;

/// <summary>Tests readonly modifier placement and stale diagnostic rejection.</summary>
public class Psh1014ReadonlyStructCodeFixUnitTest
{
    /// <summary>Verifies Fix All updates both ordinary and record structs in one document.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task MultipleStructDeclarationsAreFixedAsync() =>
        VerifyAsync(
            """
            struct {|PSH1014:First|}
            {
            }
            public record struct {|PSH1014:Second|}
            {
            }
            """,
            """
            readonly struct First
            {
            }
            public readonly record struct Second
            {
            }
            """);

    /// <summary>Verifies declarations without access modifiers preserve leading comments.</summary>
    /// <param name="declaration">The struct or record struct declaration.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("struct")]
    [Arguments("record struct")]
    public Task FirstModifierPreservesCommentAsync(string declaration)
    {
        var source = $$"""
                       // The value type.
                       {{declaration}} {|PSH1014:Value|}
                       {
                       }
                       """;
        var fixedSource = $$"""
                            // The value type.
                            readonly {{declaration}} Value
                            {
                            }
                            """;
        return VerifyAsync(source, fixedSource);
    }

    /// <summary>Verifies readonly precedes ref and unsafe while following accessibility modifiers.</summary>
    /// <param name="modifiers">The original modifiers.</param>
    /// <param name="fixedModifiers">The standard ordering after the fix.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("ref", "readonly ref")]
    [Arguments("unsafe", "readonly unsafe")]
    [Arguments("public ref", "public readonly ref")]
    [Arguments("public unsafe", "public readonly unsafe")]
    [Arguments("public unsafe ref", "public readonly unsafe ref")]
    [Arguments("public", "public readonly")]
    public Task ModifierOrderingIsPreservedAsync(string modifiers, string fixedModifiers)
    {
        var source = $$"""
                       // The value type.
                       {{modifiers}} struct {|PSH1014:Value|}
                       {
                       }
                       """;
        var fixedSource = $$"""
                            // The value type.
                            {{fixedModifiers}} struct Value
                            {
                            }
                            """;
        return VerifyAsync(source, fixedSource);
    }

    /// <summary>Verifies already-readonly structs and unrelated syntax reject stale diagnostics.</summary>
    /// <param name="source">The document with a diagnostic that no longer identifies a mutable struct.</param>
    /// <param name="cancellationToken">The test cancellation token.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("readonly struct Value { }")]
    [Arguments("readonly record struct Value { }")]
    [Arguments("class Value { }")]
    [Arguments("record Value { }")]
    [Arguments("enum Value { First }")]
    public async Task StaleDiagnosticsAreNotFixedAsync(string source, CancellationToken cancellationToken)
    {
        using var workspace = new AdhocWorkspace();
        var project = workspace.AddProject("ReadonlyStruct", LanguageNames.CSharp);
        var document = workspace.AddDocument(project.Id, "Test.cs", SourceText.From(source));
        var root = (await document.GetSyntaxRootAsync(cancellationToken))!;
        var declaration = root.DescendantNodes().OfType<BaseTypeDeclarationSyntax>().Single();
        var diagnostic = Diagnostic.Create(AllocationRules.MakeStructReadonly, declaration.Identifier.GetLocation(), "Value");
        using var container = new ContainerConfiguration().WithPart<Psh1014ReadonlyStructCodeFixProvider>().CreateContainer();
        var provider = container.GetExport<CodeFixProvider>();
        var actions = new List<CodeAction>();
        var context = new CodeFixContext(document, diagnostic, (action, _) => actions.Add(action), cancellationToken);

        await provider.RegisterCodeFixesAsync(context);
        var editor = await DocumentEditor.CreateAsync(document, cancellationToken);
        BatchEditRegistration.Register<Psh1014ReadonlyStructCodeFixProvider>(editor, diagnostic);

        await Assert.That(actions).IsEmpty();
        await Assert.That(editor.GetChangedRoot().ToFullString()).IsEqualTo(source);
    }

    /// <summary>Runs a fix verification with unsafe declarations enabled.</summary>
    /// <param name="source">The source with diagnostic markup.</param>
    /// <param name="fixedSource">The expected replacement source.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Task VerifyAsync(string source, string fixedSource) =>
        CreateTest(source, fixedSource).RunAsync(CancellationToken.None);

    /// <summary>Creates the verifier configuration for readonly and unsafe syntax.</summary>
    /// <param name="source">The source with diagnostic markup.</param>
    /// <param name="fixedSource">The expected replacement source.</param>
    /// <returns>The configured verifier.</returns>
    private static Verify.Test CreateTest(string source, string fixedSource)
    {
        var test = new Verify.Test { ReferenceAssemblies = AnalyzerFrameworks.Net90, TestCode = source, FixedCode = fixedSource };
        test.SolutionTransforms.Add(static (solution, projectId) =>
            solution.WithProjectCompilationOptions(projectId, ((CSharpCompilationOptions)solution.GetProject(projectId)!.CompilationOptions!).WithAllowUnsafe(true)));
        return test;
    }
}
