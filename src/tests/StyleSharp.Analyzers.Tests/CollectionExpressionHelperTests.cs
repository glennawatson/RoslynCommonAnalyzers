// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using RoslynCommon.Analyzers.Tests;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Tests explicit collection targets and method type inference.</summary>
public class CollectionExpressionHelperTests
{
    /// <summary>Verifies absent framework collections produce an empty target set.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task MissingFrameworkHasNoTargetsAsync()
    {
        var compilation = CSharpCompilation.Create("Empty");
        await Assert.That(CollectionExpressionHelper.ResolveTargets(compilation)).IsEmpty();
    }

    /// <summary>Verifies target acceptance respects explicit contexts and generic method inference.</summary>
    /// <param name="members">The containing members.</param>
    /// <param name="expected">Whether replacement has a conservative target.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("int[] M() => new int[] { 1 };", true)]
    [Arguments("object M() => new int[] { 1 };", false)]
    [Arguments("T M<T>() => new int[] { 1 };", false)]
    [Arguments("int[,] M() => new int[,] { { 1 } };", false)]
    [Arguments("void M() { var value = new int[] { 1 }; }", false)]
    [Arguments("int[] Value { get; } = new int[] { 1 };", false)]
    [Arguments("void M() { int[] value = new int[] { 1 }; }", true)]
    [Arguments("void M(int[] value) { value = new int[] { 1 }; }", true)]
    [Arguments("int[] M() { return new int[] { 1 }; }", true)]
    [Arguments("int M() => (new int[] { 1 }).Length;", false)]
    [Arguments("void M() { Use(new int[] { 1 }); } void Use(int[] value) { }", true)]
    [Arguments("void M() { Use(new int[] { 1 }); } void Use<T>(T value) { }", false)]
    [Arguments("void M() { Use(new int[] { 1 }); } void Use<T>(T[] value) { }", false)]
    [Arguments("void M() { Use(new int[] { 1 }); } void Use<T>(System.Collections.Generic.IEnumerable<T> value) { }", false)]
    [Arguments("void M() { Use<int>(new int[] { 1 }); } void Use<T>(System.Collections.Generic.IEnumerable<int> value) { }", true)]
    [Arguments("void M() { Use<int>(new int[] { 1 }); } void Use<T>(int[] value) { }", true)]
    [Arguments("void M() { Missing(new int[] { 1 }); }", true)]
    [Arguments("int this[int[] values] => 0; int M() => this[new int[] { 1 }];", true)]
    [Arguments("class Receiver<T> { public void Use<U>(T value) { } } void M(Receiver<int[]> receiver) { receiver.Use<int>(new int[] { 1 }); }", true)]
    [Arguments("System.Collections.Generic.IReadOnlyCollection<int> M() => new int[] { 1 };", true)]
    [Arguments("System.Collections.Generic.IList<int> M() => new int[] { 1 };", true)]
    public async Task ExplicitTargetControlsAcceptanceAsync(string members, bool expected)
    {
        var tree = CSharpSyntaxTree.ParseText($"class C {{ {members} }}");
        var compilation = CSharpCompilation.Create(nameof(Test), [tree], RuntimeMetadataReferences.Platform);
        var diagnostics = await compilation.WithAnalyzers([new TargetProbeAnalyzer(false)]).GetAnalyzerDiagnosticsAsync();
        await Assert.That(diagnostics.Length).IsEqualTo(expected ? 1 : 0);
    }

    /// <summary>Verifies an invalid return context without a converted type is rejected.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task UnresolvedConvertedTypeIsRejectedAsync()
    {
        var tree = CSharpSyntaxTree.ParseText("class C { void M() { return null; } }");
        var compilation = CSharpCompilation.Create(nameof(Test), [tree], RuntimeMetadataReferences.Platform);
        var diagnostics = await compilation.WithAnalyzers([new TargetProbeAnalyzer(true)]).GetAnalyzerDiagnosticsAsync();
        await Assert.That(diagnostics).IsEmpty();
    }

    /// <summary>Verifies only System span definitions with one type argument are recognized.</summary>
    /// <param name="type">The type to classify.</param>
    /// <param name="expected">Whether the type is a framework span.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("System.Span<int>", true)]
    [Arguments("System.ReadOnlySpan<int>", true)]
    [Arguments("Other.Span<int>", false)]
    [Arguments("Other.System.Span<int>", false)]
    [Arguments("System.Collections.Generic.List<int>", false)]
    [Arguments("int[]", false)]
    [Arguments("int", false)]
    public async Task SpanRecognitionRequiresFrameworkNamespaceAsync(string type, bool expected)
    {
        var tree = CSharpSyntaxTree.ParseText($"class C {{ {type} value; }} namespace Other {{ class Span<T> {{ }} namespace System {{ class Span<T> {{ }} }} }}");
        var compilation = CSharpCompilation.Create(nameof(Test), [tree], RuntimeMetadataReferences.Platform);
        var root = await tree.GetRootAsync();
        var declaration = root.DescendantNodes().OfType<VariableDeclarationSyntax>().Single();
        var symbol = compilation.GetSemanticModel(tree).GetTypeInfo(declaration.Type).Type;
        await Assert.That(CollectionExpressionHelper.IsSpanTarget(symbol)).IsEqualTo(expected);
        await Assert.That(CollectionExpressionHelper.IsSpanTarget(null)).IsFalse();
    }

    /// <summary>Verifies collection syntax is gated on C# 12.</summary>
    /// <param name="version">The parser language version.</param>
    /// <param name="expected">Whether collection expressions are supported.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments(LanguageVersion.CSharp11, false)]
    [Arguments(LanguageVersion.CSharp12, true)]
    public async Task LanguageVersionControlsSupportAsync(LanguageVersion version, bool expected)
    {
        var tree = CSharpSyntaxTree.ParseText("class C { }", new(version));
        await Assert.That(CollectionExpressionHelper.IsLanguageSupported(await tree.GetRootAsync())).IsEqualTo(expected);
    }

    /// <summary>Runs target checks in the real analyzer driver context.</summary>
    /// <param name="inspectConvertedType">Whether to probe converted-type resolution instead of target acceptance.</param>
    private sealed class TargetProbeAnalyzer(bool inspectConvertedType) : DiagnosticAnalyzer
    {
        /// <summary>The diagnostic emitted when a target check succeeds.</summary>
        private static readonly DiagnosticDescriptor AcceptedTarget = new("TEST0001", "Accepted target", "Accepted target", "Tests", DiagnosticSeverity.Warning, isEnabledByDefault: true);

        /// <inheritdoc/>
        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [AcceptedTarget];

        /// <inheritdoc/>
        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.ArrayCreationExpression, SyntaxKind.NullLiteralExpression);
        }

        /// <summary>Reports whether the helper resolves an explicit target in this context.</summary>
        /// <param name="context">The driver-provided syntax context.</param>
        private void Analyze(SyntaxNodeAnalysisContext context)
        {
            var expression = (ExpressionSyntax)context.Node;
            var accepted = inspectConvertedType
                ? CollectionExpressionHelper.TryGetConvertedTypeWithExplicitTarget(context, expression, out var converted) || converted is not null
                : CollectionExpressionHelper.HasAcceptedTarget(context, expression, CollectionExpressionHelper.ResolveTargets(context.Compilation));
            if (!accepted)
            {
                return;
            }

            context.ReportDiagnostic(Diagnostic.Create(AcceptedTarget, expression.GetLocation()));
        }
    }
}
