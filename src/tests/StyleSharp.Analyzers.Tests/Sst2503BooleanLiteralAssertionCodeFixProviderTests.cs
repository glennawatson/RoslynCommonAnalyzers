// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Composition.Hosting;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.CodeAnalysis.Text;
using RoslynCommon.Analyzers.Tests;

using Verify = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.Sst2503BooleanLiteralAssertionAnalyzer,
    StyleSharp.Analyzers.Sst2503BooleanLiteralAssertionCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Tests boolean-assertion fixes when diagnostics outlive the original assertion shape.</summary>
public sealed class Sst2503BooleanLiteralAssertionCodeFixProviderTests
{
    /// <summary>Verifies a parenthesized method group cannot be invoked as an equality assertion.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ParenthesizedMethodGroupHasNoFixAsync()
    {
        const string Source = """
            class C { void M(bool value) { (Xunit.Assert.Equal)(true, value); } }
            namespace Xunit
            {
                public static class Assert
                {
                    public static void Equal(bool expected, bool actual) { }
                    public static void True(bool condition) { }
                }
            }
            """;
        using var workspace = new AdhocWorkspace();
        var project = workspace.AddProject(nameof(Test), LanguageNames.CSharp).WithMetadataReferences(RuntimeMetadataReferences.Platform);
        var document = project.AddDocument("Test.cs", SourceText.From(Source));
        var root = (await document.GetSyntaxRootAsync())!;
        var cast = root.DescendantNodes().OfType<CastExpressionSyntax>().Single();
        var model = (await document.GetSemanticModelAsync())!;
        await Assert.That(model.GetSymbolInfo(cast).Symbol).IsNull();
        var diagnostic = Diagnostic.Create(TestingRules.BooleanLiteralAssertion, cast.GetLocation());
        using var container = new ContainerConfiguration().WithPart<Sst2503BooleanLiteralAssertionCodeFixProvider>().CreateContainer();
        var provider = container.GetExport<CodeFixProvider>();
        var actions = new List<CodeAction>();
        await provider.RegisterCodeFixesAsync(new(document, diagnostic, (action, _) => actions.Add(action), CancellationToken.None));
        await Assert.That(actions).IsEmpty();
        var editor = await DocumentEditor.CreateAsync(document);
        ((IBatchFixableCodeFix)provider).RegisterBatchEdits(editor, diagnostic);
        await Assert.That(editor.GetChangedRoot().ToFullString()).IsEqualTo(Source);
    }

    /// <summary>Verifies a conditional receiver is preserved while a named actual argument loses its equality name.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ConditionalAssertionPreservesReceiverAsync()
    {
        const string Source = """
            class C { void M(Xunit.Assert assertion, bool value) { assertion?.{|SST2503:Equal|}(expected: true, actual: value); } }
            namespace Xunit
            {
                public class Assert
                {
                    public void Equal(bool expected, bool actual) { }
                    public void True(bool condition) { }
                }
            }
            """;
        const string Expected = """
            class C { void M(Xunit.Assert assertion, bool value) { assertion?.True(value); } }
            namespace Xunit
            {
                public class Assert
                {
                    public void Equal(bool expected, bool actual) { }
                    public void True(bool condition) { }
                }
            }
            """;
        var test = new Verify.Test { ReferenceAssemblies = AnalyzerFrameworks.Net90, TestCode = Source, FixedCode = Expected };
        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies stale assertions receive neither an action nor batch edits.</summary>
    /// <param name="statement">The assertion or unrelated statement at the old diagnostic.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("int value = 0;")]
    [Arguments("Xunit.Assert.Equal(true, false, true);")]
    [Arguments("Xunit.Assert.Equal(1, 2);")]
    [Arguments("Missing.Equal(true, value);")]
    [Arguments("Xunit.Assert.Equal(true, value);")]
    public async Task StaleAssertionHasNoFixAsync(string statement)
    {
        var source = $$"""
            class C { void M(bool value) { {{statement}} } }
            namespace Xunit
            {
                public static class Assert
                {
                    public static void Equal<T>(T expected, T actual) { }
                    public static void Equal(bool expected, bool actual, bool message) { }
                }
            }
            """;
        using var workspace = new AdhocWorkspace();
        var project = workspace.AddProject(nameof(Test), LanguageNames.CSharp).WithMetadataReferences(RuntimeMetadataReferences.Platform);
        var document = project.AddDocument("Test.cs", SourceText.From(source));
        var root = (await document.GetSyntaxRootAsync())!;
        var target = root.DescendantNodes().OfType<MethodDeclarationSyntax>().First().Body!.Statements.Single();
        var location = target.DescendantNodes().OfType<InvocationExpressionSyntax>().FirstOrDefault()?.GetLocation() ?? target.GetLocation();
        var diagnostic = Diagnostic.Create(TestingRules.BooleanLiteralAssertion, location);
        using var container = new ContainerConfiguration().WithPart<Sst2503BooleanLiteralAssertionCodeFixProvider>().CreateContainer();
        var provider = container.GetExport<CodeFixProvider>();
        var actions = new List<CodeAction>();
        await provider.RegisterCodeFixesAsync(new(document, diagnostic, (action, _) => actions.Add(action), CancellationToken.None));
        await Assert.That(actions).IsEmpty();
        var editor = await DocumentEditor.CreateAsync(document);
        ((IBatchFixableCodeFix)provider).RegisterBatchEdits(editor, diagnostic);
        await Assert.That(editor.GetChangedRoot().ToFullString()).IsEqualTo(source);
    }
}
