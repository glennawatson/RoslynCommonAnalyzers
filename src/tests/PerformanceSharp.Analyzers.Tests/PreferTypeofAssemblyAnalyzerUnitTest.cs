// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Immutable;
using System.Runtime.CompilerServices;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

using RoslynCommon.Analyzers.Tests;

using Verify = PerformanceSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    PerformanceSharp.Analyzers.Psh1404PreferTypeofAssemblyAnalyzer,
    PerformanceSharp.Analyzers.Psh1404PreferTypeofAssemblyCodeFixProvider>;

namespace PerformanceSharp.Analyzers.Tests;

/// <summary>Tests reflection assembly lookup reporting and replacement syntax.</summary>
public class PreferTypeofAssemblyAnalyzerUnitTest
{
    /// <summary>The unqualified name shared by the nongeneric declarations.</summary>
    private const string SimpleTypeName = "C";

    /// <summary>Verifies the enclosing declaration supplies the typeof operand.</summary>
    /// <param name="declaration">The containing type declaration.</param>
    /// <param name="typeName">The type name used by the replacement.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("class C", SimpleTypeName)]
    [Arguments("struct C", SimpleTypeName)]
    [Arguments("record C", SimpleTypeName)]
    [Arguments("class C<T>", "C<T>")]
    [Arguments("class C<TKey, TValue>", "C<TKey, TValue>")]
    public Task EnclosingTypeSuppliesTypeofOperandAsync(string declaration, string typeName)
    {
        var source = $$"""
                       using System.Reflection;
                       {{declaration}}
                       {
                           public static Assembly M() => {|PSH1404:Assembly.GetExecutingAssembly()|};
                       }
                       """;
        var fixedSource = $$"""
                            using System.Reflection;
                            {{declaration}}
                            {
                                public static Assembly M() => typeof({{typeName}}).Assembly;
                            }
                            """;
        return VerifyAsync(source, fixedSource);
    }

    /// <summary>Verifies using-static calls and nested generic declarations use the nearest type.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task StaticImportInsideNestedGenericTypeIsFixedAsync() =>
        VerifyAsync(
            """
            using System.Reflection;
            using static System.Reflection.Assembly;
            class Outer<T>
            {
                class Inner<TKey, TValue>
                {
                    public Assembly M() => {|PSH1404:GetExecutingAssembly()|};
                }
            }
            """,
            """
            using System.Reflection;
            using static System.Reflection.Assembly;
            class Outer<T>
            {
                class Inner<TKey, TValue>
                {
                    public Assembly M() => typeof(Inner<TKey, TValue>).Assembly;
                }
            }
            """);

    /// <summary>Verifies Fix All preserves comments beside every replaced invocation.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task MultipleLookupsPreserveCommentsAsync() =>
        VerifyAsync(
            """
            using System.Reflection;
            class C
            {
                public Assembly First() => /* before */ {|PSH1404:Assembly.GetExecutingAssembly()|} /* after */;
                public Assembly Second() => {|PSH1404:global::System.Reflection.Assembly.GetExecutingAssembly()|};
            }
            """,
            """
            using System.Reflection;
            class C
            {
                public Assembly First() => /* before */ typeof(C).Assembly /* after */;
                public Assembly Second() => typeof(C).Assembly;
            }
            """);

    /// <summary>Verifies lookalikes and unresolved calls do not report the reflection rule.</summary>
    /// <param name="source">A call that differs in syntax or binding.</param>
    /// <param name="cancellationToken">The test cancellation token.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("class C { object M() => System.Reflection.Assembly.GetCallingAssembly(); }")]
    [Arguments("class C { object M() => System.Reflection.Assembly.GetExecutingAssembly(1); }")]
    [Arguments("class C { object M() => Missing.GetExecutingAssembly(); }")]
    [Arguments("class C { static object GetExecutingAssembly() => null; object M() => GetExecutingAssembly(); }")]
    [Arguments("class C { object GetExecutingAssembly() => null; object M() => GetExecutingAssembly(); }")]
    [Arguments("class C { static object GetExecutingAssembly(int value = 0) => null; object M() => GetExecutingAssembly(); }")]
    [Arguments("class C { object M(System.Func<object> GetExecutingAssembly) => GetExecutingAssembly(); }")]
    [Arguments("class C { object M(System.Func<object> GetExecutingAssembly) => (GetExecutingAssembly)(); }")]
    [Arguments("class C { object GetExecutingAssembly() => null; object M(C other) => other?.GetExecutingAssembly(); }")]
    [Arguments("class C { static object GetExecutingAssembly<T>() => null; object M() => GetExecutingAssembly<int>(); }")]
    public async Task DifferentInvocationShapesAreCleanAsync(string source, CancellationToken cancellationToken)
    {
        var diagnostics = await AnalyzeAsync(source, RuntimeMetadataReferences.Platform, cancellationToken);
        await Assert.That(diagnostics).IsEmpty();
    }

    /// <summary>Verifies absent and incompatible framework APIs disable the rule.</summary>
    /// <param name="members">The available reflection assembly members.</param>
    /// <param name="cancellationToken">The test cancellation token.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("")]
    [Arguments("public Assembly GetExecutingAssembly() => null;")]
    [Arguments("public static Assembly GetExecutingAssembly(int value) => null;")]
    [Arguments("public static System.Func<Assembly> GetExecutingAssembly;")]
    public async Task FrameworkWithoutStaticParameterlessFactoryIsCleanAsync(string members, CancellationToken cancellationToken)
    {
        var source = $$"""
                       namespace System.Reflection { public class Assembly { {{members}} } }
                       class C { object M() => System.Reflection.Assembly.GetExecutingAssembly(); }
                       """;
        var diagnostics = await AnalyzeAsync(source, RuntimeMetadataReferences.Platform, cancellationToken);
        await Assert.That(diagnostics).IsEmpty();
    }

    /// <summary>Verifies the API search skips overloads before finding the static factory.</summary>
    /// <param name="cancellationToken">The test cancellation token.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task FactoryAfterAnOverloadStillReportsAsync(CancellationToken cancellationToken)
    {
        const string Source = """
                              namespace System.Reflection
                              {
                                  public class Assembly
                                  {
                                      public static Assembly GetExecutingAssembly(int value) => null;
                                      public static Assembly GetExecutingAssembly() => null;
                                  }
                              }
                              class C { object M() => System.Reflection.Assembly.GetExecutingAssembly(); }
                              """;
        var diagnostics = await AnalyzeAsync(Source, RuntimeMetadataReferences.Platform, cancellationToken);
        await Assert.That(diagnostics).Count().IsEqualTo(1);
        await Assert.That(diagnostics[0].Id).IsEqualTo("PSH1404");
    }

    /// <summary>Verifies a compilation without reflection metadata remains clean.</summary>
    /// <param name="cancellationToken">The test cancellation token.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task MissingAssemblyTypeIsCleanAsync(CancellationToken cancellationToken)
    {
        const string Source = "class C { object M() => System.Reflection.Assembly.GetExecutingAssembly(); }";
        var diagnostics = await AnalyzeAsync(Source, [], cancellationToken);
        await Assert.That(diagnostics).IsEmpty();
    }

    /// <summary>Verifies calls without a declared containing type use the synthesized program name.</summary>
    /// <param name="source">A global call or assembly attribute argument.</param>
    /// <param name="cancellationToken">The test cancellation token.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("System.Reflection.Assembly.GetExecutingAssembly();")]
    [Arguments("[assembly: System.Obsolete(System.Reflection.Assembly.GetExecutingAssembly().ToString())]")]
    public async Task CallsOutsideDeclaredTypesUseProgramNameAsync(string source, CancellationToken cancellationToken)
    {
        var diagnostics = await AnalyzeAsync(source, RuntimeMetadataReferences.Platform, cancellationToken);
        await Assert.That(diagnostics).Count().IsEqualTo(1);
        await Assert.That(diagnostics[0].GetMessage()).Contains("Program");
    }

    /// <summary>Runs a code-fix verification on a framework that supplies reflection.</summary>
    /// <param name="source">The source with diagnostic markup.</param>
    /// <param name="fixedSource">The expected replacement source.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Task VerifyAsync(string source, string fixedSource) =>
        new Verify.Test { ReferenceAssemblies = AnalyzerFrameworks.Net90, TestCode = source, FixedCode = fixedSource }.RunAsync(CancellationToken.None);

    /// <summary>Runs the analyzer against intentionally incomplete or alternate framework sources.</summary>
    /// <param name="source">The compilation source.</param>
    /// <param name="references">Cached runtime references, or none for a missing framework.</param>
    /// <param name="cancellationToken">The test cancellation token.</param>
    /// <returns>The diagnostics emitted by the analyzer.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Task<ImmutableArray<Diagnostic>> AnalyzeAsync(string source, ImmutableArray<MetadataReference> references, CancellationToken cancellationToken) =>
        CSharpCompilation.Create(
                "AssemblyLookupTests",
                [CSharpSyntaxTree.ParseText(source, cancellationToken: cancellationToken)],
                references,
                new(OutputKind.DynamicallyLinkedLibrary))
            .WithAnalyzers([new Psh1404PreferTypeofAssemblyAnalyzer()])
            .GetAnalyzerDiagnosticsAsync(cancellationToken);
}
