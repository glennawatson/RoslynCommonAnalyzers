// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using RoslynCommon.Analyzers.Tests;

namespace PerformanceSharp.Analyzers.Tests;

/// <summary>Tests synchronous-signature exemption boundaries in real syntax contexts.</summary>
public class BlockingWaitExemptionTests
{
    /// <summary>Verifies top-level and type-header expressions have no replaceable member signature.</summary>
    /// <param name="source">The source containing one invocation.</param>
    /// <param name="expected">Whether the helper exempts the invocation.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("System.Threading.Tasks.Task.CompletedTask.Wait();", true)]
    [Arguments("class B { public B(int value) { } } class C() : B(System.Threading.Tasks.Task.FromResult(1).Result) { }", true)]
    [Arguments("class C { int value = System.Threading.Tasks.Task.FromResult(1).Result; }", false)]
    [Arguments("class C { int Value => System.Threading.Tasks.Task.FromResult(1).Result; }", false)]
    public async Task EnclosingDeclarationControlsExemptionAsync(string source, bool expected)
    {
        var tree = CSharpSyntaxTree.ParseText(source);
        var compilation = CSharpCompilation.Create(nameof(Test), [tree], RuntimeMetadataReferences.Platform);
        var diagnostics = await compilation.WithAnalyzers([new ExemptionProbeAnalyzer(true)]).GetAnalyzerDiagnosticsAsync();
        await Assert.That(diagnostics.Length).IsEqualTo(expected ? 1 : 0);
        await Assert.That(diagnostics.All(static diagnostic => diagnostic.Id == "TEST0001")).IsTrue();
    }

    /// <summary>Verifies a detached expression is treated as having no changeable enclosing member.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task DetachedExpressionHasNoEnclosingMemberAsync()
    {
        var expression = SyntaxFactory.ParseExpression("pending.Wait()");
        await Assert.That(BlockingWaitExemption.IsExempt(default, expression, null, null)).IsTrue();
    }

    /// <summary>Verifies the awaiter exemption requires the marker interface and an IsCompleted property.</summary>
    /// <param name="interfaces">The interfaces implemented by the enclosing type.</param>
    /// <param name="completionMember">The candidate IsCompleted member.</param>
    /// <param name="resolveMarker">Whether the helper receives the framework marker.</param>
    /// <param name="expected">Whether GetResult is exempt.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("INotifyCompletion", "public bool IsCompleted => true;", true, true)]
    [Arguments("IDisposable, INotifyCompletion", "public bool IsCompleted => true;", true, true)]
    [Arguments("IDisposable", "public bool IsCompleted => true;", true, false)]
    [Arguments("INotifyCompletion", "", true, false)]
    [Arguments("INotifyCompletion", "public bool IsCompleted;", true, false)]
    [Arguments("INotifyCompletion", "public bool IsCompleted() => true;", true, false)]
    [Arguments("INotifyCompletion", "public bool IsCompleted => true;", false, false)]
    [Arguments("INotifyCompletion", "public int IsCompleted => 1;", true, true)]
    public async Task AwaiterShapeControlsExemptionAsync(string interfaces, string completionMember, bool resolveMarker, bool expected)
    {
        var source = $$"""
            using System;
            using System.Runtime.CompilerServices;
            using System.Threading.Tasks;
            class C : {{interfaces}}
            {
                {{completionMember}}
                public void OnCompleted(Action action) { }
                public void Dispose() { }
                public void GetResult(Task pending) => pending.Wait();
            }
            """;
        var tree = CSharpSyntaxTree.ParseText(source);
        var compilation = CSharpCompilation.Create(nameof(Test), [tree], RuntimeMetadataReferences.Platform);
        var diagnostics = await compilation.WithAnalyzers([new ExemptionProbeAnalyzer(resolveMarker)]).GetAnalyzerDiagnosticsAsync();
        await Assert.That(diagnostics.Length).IsEqualTo(expected ? 1 : 0);
        await Assert.That(diagnostics.All(static diagnostic => diagnostic.Id == "TEST0001")).IsTrue();
    }

    /// <summary>Reports invocations that the exemption helper accepts.</summary>
    /// <param name="resolveMarker">Whether to provide the awaiter marker interface.</param>
    private sealed class ExemptionProbeAnalyzer(bool resolveMarker) : DiagnosticAnalyzer
    {
        /// <summary>The diagnostic emitted for an exempt invocation.</summary>
        private static readonly DiagnosticDescriptor ExemptInvocation = new("TEST0001", "Exempt invocation", "Exempt invocation", "Tests", DiagnosticSeverity.Warning, isEnabledByDefault: true);

        /// <inheritdoc/>
        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [ExemptInvocation];

        /// <inheritdoc/>
        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.InvocationExpression);
        }

        /// <summary>Calls the helper with the driver's semantic context.</summary>
        /// <param name="context">The invocation analysis context.</param>
        private void Analyze(SyntaxNodeAnalysisContext context)
        {
            var marker = resolveMarker ? context.Compilation.GetTypeByMetadataName("System.Runtime.CompilerServices.INotifyCompletion") : null;
            if (BlockingWaitExemption.IsExempt(context, context.Node, marker, context.Compilation.GetEntryPoint(context.CancellationToken)))
            {
                context.ReportDiagnostic(Diagnostic.Create(ExemptInvocation, context.Node.GetLocation()));
            }
        }
    }
}
