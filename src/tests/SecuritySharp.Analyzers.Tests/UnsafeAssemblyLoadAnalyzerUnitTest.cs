// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using RoslynCommon.Analyzers.Tests;

using AnalyzeLoad = SecuritySharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    SecuritySharp.Analyzers.Ses1402UnsafeAssemblyLoadAnalyzer>;

namespace SecuritySharp.Analyzers.Tests;

/// <summary>Unit tests for SES1402 (do not load an assembly from raw bytes or a non-constant location).</summary>
public class UnsafeAssemblyLoadAnalyzerUnitTest
{
    /// <summary>Cached references for minimal assembly-loading contracts.</summary>
    private static readonly ImmutableArray<MetadataReference> CoreReferences = [RuntimeMetadataReferences.CoreLibrary];

    /// <summary>Verifies parentheses and null-forgiving operators preserve trusted embedded-resource recognition.</summary>
    /// <param name="source">The stream supplied to the loader.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("((host.GetManifestResourceStream(\"plugin\"))!)")]
    [Arguments("(GetManifestResourceStream(\"plugin\"))!")]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task WrappedResourceStreamIsCleanAsync(string source) =>
        VerifyNet90Async(
            $$"""
            using System.IO;
            using System.Reflection;
            using System.Runtime.Loader;
            class C
            {
                Assembly M(AssemblyLoadContext context, Assembly host) => context.LoadFromStream({{source}});
                static Stream GetManifestResourceStream(string name) => Stream.Null;
            }
            """);

    /// <summary>Verifies reordered named arguments identify the assembly stream rather than the symbols stream.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task ReorderedStreamArgumentsUseAssemblySourceAsync() =>
        VerifyNet90Async(
            """
            using System.IO;
            using System.Reflection;
            using System.Runtime.Loader;
            class C
            {
                Assembly M(AssemblyLoadContext context, Assembly host, Stream stream)
                    => {|SES1402:context.LoadFromStream(assemblySymbols: host.GetManifestResourceStream("symbols"), assembly: stream)|};
                Assembly N(AssemblyLoadContext context, Assembly host, Stream stream)
                    => context.LoadFromStream(assemblySymbols: stream, assembly: host.GetManifestResourceStream("plugin"));
            }
            """);

    /// <summary>Verifies unresolved overloads, parameterless calls, and unrelated stream loaders are ignored.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task NonAssemblyAndUnresolvedCallsAreCleanAsync() =>
        VerifyNet90Async(
            """
            using System.Reflection;
            class C
            {
                object M() => Assembly.Load({|CS1503:1|});
                object N() => C.Load();
                object P() => C.LoadFromStream(new object());
                static object Load() => null;
                static object LoadFromStream(object value) => value;
            }
            """);

    /// <summary>Verifies a framework without load contexts still distinguishes raw assembly loads from unrelated stream loaders.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task MissingLoadContextTypeIsCleanAsync() =>
        new AnalyzeLoad.Test
        {
            ReferenceAssemblies = AnalyzerFrameworks.Net462,
            TestCode = """
                using System.Reflection;
                class Loader { public static object LoadFromStream(object stream) => stream; }
                class C
                {
                    object M() => Loader.LoadFromStream(null);
                    Assembly N(byte[] bytes) => {|SES1402:Assembly.Load(bytes)|};
                }
                """,
        }.RunAsync(CancellationToken.None);

    /// <summary>Verifies a loader cannot be classified without reflection assembly metadata.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task MissingAssemblyMetadataIsCleanAsync()
    {
        var tree = CSharpSyntaxTree.ParseText("class C { object M() => Loader.Load(new byte[0]); }");
        var compilation = CSharpCompilation.Create("MissingAssembly", [tree]);
        var diagnostics = await compilation.WithAnalyzers([new Ses1402UnsafeAssemblyLoadAnalyzer()]).GetAnalyzerDiagnosticsAsync();
        await Assert.That(diagnostics).IsEmpty();
    }

    /// <summary>Verifies an omitted optional source and an unrecognized assembly method do not report.</summary>
    /// <param name="method">The available assembly method.</param>
    /// <param name="call">The invocation to analyze.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("public static object LoadFrom(string path = null, bool flag = false) => null;", "Assembly.LoadFrom(flag: true)")]
    [Arguments("public static object LoadFromStream(object stream) => null;", "Assembly.LoadFromStream(null)")]
    [Arguments("public static object Load(int[] bytes) => null;", "Assembly.Load(new int[0])")]
    public async Task UnsupportedAssemblyContractIsCleanAsync(string method, string call)
    {
        var source = $$"""
            using System.Reflection;
            namespace System.Reflection { class Assembly { {{method}} } }
            class C { object M() => {{call}}; }
            """;
        var compilation = CSharpCompilation.Create("AssemblyContract", [CSharpSyntaxTree.ParseText(source)], CoreReferences);
        var diagnostics = await compilation.WithAnalyzers([new Ses1402UnsafeAssemblyLoadAnalyzer()]).GetAnalyzerDiagnosticsAsync();
        await Assert.That(diagnostics).IsEmpty();
    }

    /// <summary>Verifies <c>Assembly.Load(byte[])</c> on a raw buffer is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task RawBytesLoadReportedAsync() =>
        VerifyNet90Async(
            """
            using System.Reflection;

            public class C
            {
                public Assembly M(byte[] blob) => {|SES1402:Assembly.Load(blob)|};
            }
            """);

    /// <summary>Verifies the raw-bytes overload passed by name is still reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task RawBytesLoadNamedArgumentReportedAsync() =>
        VerifyNet90Async(
            """
            using System.Reflection;

            public class C
            {
                public Assembly M(byte[] blob) => {|SES1402:Assembly.Load(rawAssembly: blob)|};
            }
            """);

    /// <summary>Verifies <c>AssemblyLoadContext.LoadFromStream</c> is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task LoadFromStreamReportedAsync() =>
        VerifyNet90Async(
            """
            using System.IO;
            using System.Reflection;
            using System.Runtime.Loader;

            public class C
            {
                public Assembly M(AssemblyLoadContext context, Stream stream) => {|SES1402:context.LoadFromStream(stream)|};
            }
            """);

    /// <summary>Verifies <c>Assembly.LoadFrom</c> with a non-constant path is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task LoadFromNonConstantPathReportedAsync() =>
        VerifyNet90Async(
            """
            using System.Reflection;

            public class C
            {
                public Assembly M(string path) => {|SES1402:Assembly.LoadFrom(path)|};
            }
            """);

    /// <summary>Verifies <c>Assembly.LoadFile</c> with a non-constant path is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task LoadFileNonConstantPathReportedAsync() =>
        VerifyNet90Async(
            """
            using System.Reflection;

            public class C
            {
                public Assembly M(string path) => {|SES1402:Assembly.LoadFile(path)|};
            }
            """);

    /// <summary>Verifies <c>Assembly.UnsafeLoadFrom</c> with a non-constant path is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task UnsafeLoadFromNonConstantPathReportedAsync() =>
        VerifyNet90Async(
            """
            using System.Reflection;

            public class C
            {
                public Assembly M(string path) => {|SES1402:Assembly.UnsafeLoadFrom(path)|};
            }
            """);

    /// <summary>Verifies <c>Assembly.LoadFrom</c> with a string concatenation of a non-constant is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task LoadFromConcatenatedPathReportedAsync() =>
        VerifyNet90Async(
            """
            using System.Reflection;

            public class C
            {
                public Assembly M(string name) => {|SES1402:Assembly.LoadFrom("plugins/" + name + ".dll")|};
            }
            """);

    /// <summary>Verifies the safe <c>Assembly.Load(string)</c> identity overload is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task LoadByNameIsCleanAsync() =>
        VerifyNet90Async(
            """
            using System.Reflection;

            public class C
            {
                public Assembly M() => Assembly.Load("System.Text.Json");
            }
            """);

    /// <summary>Verifies the safe <c>Assembly.Load(AssemblyName)</c> identity overload is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task LoadByAssemblyNameIsCleanAsync() =>
        VerifyNet90Async(
            """
            using System.Reflection;

            public class C
            {
                public Assembly M(AssemblyName name) => Assembly.Load(name);
            }
            """);

    /// <summary>Verifies a constant literal path to <c>Assembly.LoadFrom</c> is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task LoadFromConstantPathIsCleanAsync() =>
        VerifyNet90Async(
            """
            using System.Reflection;

            public class C
            {
                public Assembly M() => Assembly.LoadFrom("plugins/known.dll");
            }
            """);

    /// <summary>Verifies a constant-field path to <c>Assembly.LoadFile</c> is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task LoadFileConstFieldPathIsCleanAsync() =>
        VerifyNet90Async(
            """
            using System.Reflection;

            public class C
            {
                private const string Path = "plugins/known.dll";

                public Assembly M() => Assembly.LoadFile(Path);
            }
            """);

    /// <summary>Verifies a stream taken directly from an embedded manifest resource is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task LoadFromStreamOfManifestResourceIsCleanAsync() =>
        VerifyNet90Async(
            """
            using System.Reflection;
            using System.Runtime.Loader;

            public class C
            {
                public Assembly M(AssemblyLoadContext context, Assembly host)
                    => context.LoadFromStream(host.GetManifestResourceStream("Embedded.Plugin.dll"));
            }
            """);

    /// <summary>Verifies a same-named <c>Load(byte[])</c> on an unrelated type is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task UnrelatedLoadMethodIsCleanAsync() =>
        VerifyNet90Async(
            """
            public sealed class Cache
            {
                public static object Load(byte[] data) => data;
            }

            public class C
            {
                public object M(byte[] data) => Cache.Load(data);
            }
            """);

    /// <summary>Runs an analyzer-only verification against the .NET 9 reference assemblies.</summary>
    /// <param name="source">The source with diagnostic markup.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task VerifyNet90Async(string source)
    {
        var test = new AnalyzeLoad.Test { ReferenceAssemblies = AnalyzerFrameworks.Net90, TestCode = source };

        await test.RunAsync(CancellationToken.None);
    }
}
