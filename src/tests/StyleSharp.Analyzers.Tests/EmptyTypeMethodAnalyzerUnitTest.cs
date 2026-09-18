// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using RoslynCommon.Analyzers.Tests;
using VerifyEmpty = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    StyleSharp.Analyzers.EmptyCodeAnalyzer>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for the opt-in empty-construct rules SST1436, SST1437, and SST1438.</summary>
public class EmptyTypeMethodAnalyzerUnitTest
{
    /// <summary>Verifies compiler markers remain valid unless empty-type checking is explicitly enabled.</summary>
    /// <param name="enabled">Whether the project opts into empty-type checking.</param>
    /// <param name="shape">The marker declaration shape.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [MatrixDataSource]
    public async Task CompilerMarkerRequiresOptInAsync(
        [Matrix(false, true)] bool enabled,
        [Matrix("static class", "class", "struct")] string shape)
    {
        var source = $$"""
            namespace System.Runtime.CompilerServices
            {
                internal {{shape}} IsExternalInit { }
            }
            """;
        var options = new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary);
        if (enabled)
        {
            options = options.WithSpecificDiagnosticOptions(new Dictionary<string, ReportDiagnostic> { ["SST1436"] = ReportDiagnostic.Warn });
        }

        var compilation = CSharpCompilation.Create("CompilerMarker", [CSharpSyntaxTree.ParseText(source)], [RuntimeMetadataReferences.CoreLibrary], options);
        await Assert.That(compilation.GetDiagnostics().Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)).IsEmpty();
        var diagnostics = await compilation.WithAnalyzers([new EmptyCodeAnalyzer()]).GetAnalyzerDiagnosticsAsync();
        await Assert.That(diagnostics.Length).IsEqualTo(enabled ? 1 : 0);
    }

    /// <summary>Verifies an empty class, interface, and method are each reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task EmptyConstructsReportedAsync() =>
        VerifyEmpty.VerifyAnalyzerAsync(
            """
            public class {|SST1436:Empty|}
            {
            }

            public interface {|SST1437:IEmpty|}
            {
            }

            public class Holder
            {
                public void {|SST1438:NoOp|}()
                {
                }
            }
            """);

    /// <summary>Verifies populated types and empty virtual/override hooks are not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task PopulatedAndExcludedConstructsAreCleanAsync() =>
        VerifyEmpty.VerifyAnalyzerAsync(
            """
            public class Populated
            {
                public int Value;
            }

            public interface IWork
            {
                void Run();
            }

            public class Base
            {
                public virtual void Hook()
                {
                }
            }

            public class Derived : Base
            {
                public override void Hook()
                {
                }
            }
            """);

    /// <summary>Verifies an empty method documented with a comment is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task DocumentedEmptyMethodIsCleanAsync() =>
        VerifyEmpty.VerifyAnalyzerAsync(
            """
            public class C
            {
                public void Configure()
                {
                    // No configuration is needed for the default pipeline.
                }
            }
            """);

    /// <summary>Verifies an empty method that implements an interface member (a no-op) is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task EmptyInterfaceImplementationIsCleanAsync() =>
        VerifyEmpty.VerifyAnalyzerAsync(
            """
            public sealed class NullScope : System.IDisposable
            {
                public void Dispose()
                {
                }
            }
            """);
}
