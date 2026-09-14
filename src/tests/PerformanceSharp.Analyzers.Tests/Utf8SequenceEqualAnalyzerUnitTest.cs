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
using Microsoft.CodeAnalysis.Testing;
using RoslynCommon.Analyzers.Tests;

using Verify = PerformanceSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    PerformanceSharp.Analyzers.Psh1210Utf8SequenceEqualAnalyzer,
    PerformanceSharp.Analyzers.Psh1210Utf8SequenceEqualCodeFixProvider>;

namespace PerformanceSharp.Analyzers.Tests;

/// <summary>Tests for <see cref="Psh1210Utf8SequenceEqualAnalyzer"/> (PSH1210 UTF-8 byte comparison).</summary>
public class Utf8SequenceEqualAnalyzerUnitTest
{
    /// <summary>Checks stale comparison diagnostics are rejected by registration and batch application.</summary>
    /// <param name="expression">The expression that no longer supports the fix.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("true")]
    [Arguments("expected == \"ok\"")]
    [Arguments("Encoding.UTF8.GetString(payload) == expected")]
    [Arguments("Encoding.UTF8.GetString(payload) == null")]
    [Arguments("Encoding.UTF8.GetString(payload) == \"\\uFFFD\"")]
    [Arguments("Encoding.UTF8.GetString(payload) == \"\\uD800\"")]
    public async Task StaleComparisonDoesNotRegisterOrEditAsync(string expression)
    {
        using var workspace = new AdhocWorkspace();
        var document = workspace.AddProject(nameof(Test), LanguageNames.CSharp)
            .WithMetadataReferences(RuntimeMetadataReferences.Platform)
            .AddDocument("Test.cs", $"using System.Text; class C {{ bool M(byte[] payload, string expected) => {expression}; }}");
        var root = (await document.GetSyntaxRootAsync())!;
        var node = root.DescendantNodes().OfType<ArrowExpressionClauseSyntax>().Single().Expression;
        var diagnostic = Diagnostic.Create(StringRules.UseUtf8SequenceEqual, node.GetLocation());
        using var container = new ContainerConfiguration().WithPart<Psh1210Utf8SequenceEqualCodeFixProvider>().CreateContainer();
        var provider = container.GetExport<CodeFixProvider>();
        var actions = new List<CodeAction>();
        await provider.RegisterCodeFixesAsync(new(document, diagnostic, (action, _) => actions.Add(action), CancellationToken.None));
        await Assert.That(actions).IsEmpty();
        var editor = await DocumentEditor.CreateAsync(document);
        ((IBatchFixableCodeFix)provider).RegisterBatchEdits(editor, diagnostic);
        await Assert.That(editor.GetChangedRoot().ToFullString()).IsEqualTo(root.ToFullString());
    }

    /// <summary>Checks constant rendering, receiver grouping, and qualified calls produce compiling edits.</summary>
    /// <param name="imports">The imports and optional shadowing declaration.</param>
    /// <param name="bytes">The byte source expression.</param>
    /// <param name="constant">The constant comparison operand.</param>
    /// <param name="expected">The expected replacement expression.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("", "payload", "\"ok\"", "global::System.MemoryExtensions.SequenceEqual(payload, \"ok\"u8)")]
    [Arguments("class MemoryExtensions {}", "payload", "\"ok\"", "global::System.MemoryExtensions.SequenceEqual(payload, \"ok\"u8)")]
    [Arguments("namespace MemoryExtensions {}", "payload", "\"ok\"", "global::System.MemoryExtensions.SequenceEqual(payload, \"ok\"u8)")]
    [Arguments("using Outer.System; namespace Outer.System { class MemoryExtensions {} }", "payload", "\"ok\"", "global::System.MemoryExtensions.SequenceEqual(payload, \"ok\"u8)")]
    [Arguments("using System;", "payload ?? other", "Expected", "(payload ?? other).AsSpan().SequenceEqual(\"o\\\"k\"u8)")]
    [Arguments("using System;", "flag ? payload : other", "\"ok\"", "(flag ? payload : other).AsSpan().SequenceEqual(\"ok\"u8)")]
    [Arguments("using System;", "(ReadOnlySpan<byte>)payload", "\"ok\"", "((ReadOnlySpan<byte>)payload).SequenceEqual(\"ok\"u8)")]
    [Arguments("using System;", "(payload)", "\"o\" + \"k\"", "(payload).AsSpan().SequenceEqual(\"ok\"u8)")]
    [Arguments("using System;", "new byte[0]", "@\"ok\"", "(new byte[0]).AsSpan().SequenceEqual(@\"ok\"u8)")]
    [Arguments("using System;", "C.Bytes", "\"ok\"", "C.Bytes.AsSpan().SequenceEqual(\"ok\"u8)")]
    [Arguments("using System;", "Array.Empty<byte>()", "\"ok\"", "Array.Empty<byte>().AsSpan().SequenceEqual(\"ok\"u8)")]
    [Arguments("using System;", "buffers[0]", "\"ok\"", "buffers[0].AsSpan().SequenceEqual(\"ok\"u8)")]
    public async Task ComparisonRewritePreservesBindingAsync(string imports, string bytes, string constant, string expected)
    {
        using var workspace = new AdhocWorkspace();
        var source = $$"""
            using System.Text;
            {{imports}}
            class C
            {
                const string Expected = "o\"k";
                static byte[] Bytes = System.Array.Empty<byte>();
                bool M(byte[] payload, byte[] other, byte[][] buffers, bool flag) => Encoding.UTF8.GetString({{bytes}}) == {{constant}};
            }
            """;
        var project = workspace.AddProject(nameof(Test), LanguageNames.CSharp)
            .WithMetadataReferences(RuntimeMetadataReferences.Platform)
            .WithParseOptions(new CSharpParseOptions(LanguageVersion.CSharp11))
            .WithCompilationOptions(new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        var document = project.AddDocument("Test.cs", source);
        var root = (await document.GetSyntaxRootAsync())!;
        var comparison = root.DescendantNodes().OfType<ArrowExpressionClauseSyntax>().Single().Expression;
        var diagnostic = Diagnostic.Create(StringRules.UseUtf8SequenceEqual, comparison.GetLocation());
        using var container = new ContainerConfiguration().WithPart<Psh1210Utf8SequenceEqualCodeFixProvider>().CreateContainer();
        var provider = container.GetExport<CodeFixProvider>();
        var actions = new List<CodeAction>();
        await provider.RegisterCodeFixesAsync(new(document, diagnostic, (action, _) => actions.Add(action), CancellationToken.None));
        await Assert.That(actions.Count).IsEqualTo(1);
        var operations = await actions[0].GetOperationsAsync(CancellationToken.None);
        var changed = operations.OfType<ApplyChangesOperation>().Single().ChangedSolution.GetDocument(document.Id)!;
        var changedRoot = (await changed.GetSyntaxRootAsync())!;
        var replacement = changedRoot.DescendantNodes().OfType<ArrowExpressionClauseSyntax>().Single().Expression;
        await Assert.That(SyntaxFactory.AreEquivalent(replacement, SyntaxFactory.ParseExpression(expected))).IsTrue();
        var compilation = (await changed.Project.GetCompilationAsync())!;
        await Assert.That(compilation.GetDiagnostics().Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error).ToArray()).IsEmpty();
        var editor = await DocumentEditor.CreateAsync(document);
        ((IBatchFixableCodeFix)provider).RegisterBatchEdits(editor, diagnostic);
        await Assert.That(SyntaxFactory.AreEquivalent(editor.GetChangedRoot(), changedRoot)).IsTrue();
    }

    /// <summary>Verifies a decoded array comparison is flagged and rewritten through AsSpan.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ArrayDecodeComparisonIsFlaggedAndFixedAsync()
    {
        const string Source = """
                              using System;
                              using System.Text;

                              public class C
                              {
                                  public bool M(byte[] payload) => {|PSH1210:Encoding.UTF8.GetString(payload) == "ok"|};
                              }
                              """;
        const string FixedSource = """
                                   using System;
                                   using System.Text;

                                   public class C
                                   {
                                       public bool M(byte[] payload) => payload.AsSpan().SequenceEqual("ok"u8);
                                   }
                                   """;
        await VerifyAsync(Source, FixedSource);
    }

    /// <summary>Verifies a span decode inequality is flagged and gains a negation.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SpanDecodeInequalityIsFlaggedAndFixedAsync()
    {
        const string Source = """
                              using System;
                              using System.Text;

                              public class C
                              {
                                  public bool M(ReadOnlySpan<byte> payload) => {|PSH1210:"ok" != Encoding.UTF8.GetString(payload)|};
                              }
                              """;
        const string FixedSource = """
                                   using System;
                                   using System.Text;

                                   public class C
                                   {
                                       public bool M(ReadOnlySpan<byte> payload) => !payload.SequenceEqual("ok"u8);
                                   }
                                   """;
        await VerifyAsync(Source, FixedSource);
    }

    /// <summary>Verifies comparing two decoded strings stays clean; only constants qualify.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task NonConstantComparisonIsCleanAsync() =>
        VerifyAsync(
            """
            using System.Text;

            public class C
            {
                public bool M(byte[] payload, string expected) => Encoding.UTF8.GetString(payload) == expected;
            }
            """);

    /// <summary>Verifies a constant containing the replacement character stays clean; invalid decodes could alias it.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task ReplacementCharacterConstantIsCleanAsync() =>
        VerifyAsync(
            """
            using System.Text;

            public class C
            {
                public bool M(byte[] payload) => Encoding.UTF8.GetString(payload) == "�";
            }
            """);

    /// <summary>Runs a verification against the .NET 9 reference assemblies.</summary>
    /// <param name="source">The test source.</param>
    /// <param name="fixedSource">The expected fixed source, when a fix should apply.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task VerifyAsync(string source, string? fixedSource = null)
    {
        var test = new Verify.Test { ReferenceAssemblies = ReferenceAssemblies.Net.Net90, TestCode = source, };
        if (fixedSource is not null)
        {
            test.FixedCode = fixedSource;
        }

        await test.RunAsync(CancellationToken.None);
    }
}
