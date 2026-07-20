// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Testing;

using AnalyzeLoad = SecuritySharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    SecuritySharp.Analyzers.Ses1402UnsafeAssemblyLoadAnalyzer>;

namespace SecuritySharp.Analyzers.Tests;

/// <summary>Unit tests for SES1402 (do not load an assembly from raw bytes or a non-constant location).</summary>
public class UnsafeAssemblyLoadAnalyzerUnitTest
{
    /// <summary>Verifies <c>Assembly.Load(byte[])</c> on a raw buffer is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task RawBytesLoadReportedAsync()
        => await VerifyNet90Async(
            """
            using System.Reflection;

            public class C
            {
                public Assembly M(byte[] blob) => {|SES1402:Assembly.Load(blob)|};
            }
            """);

    /// <summary>Verifies the raw-bytes overload passed by name is still reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task RawBytesLoadNamedArgumentReportedAsync()
        => await VerifyNet90Async(
            """
            using System.Reflection;

            public class C
            {
                public Assembly M(byte[] blob) => {|SES1402:Assembly.Load(rawAssembly: blob)|};
            }
            """);

    /// <summary>Verifies <c>AssemblyLoadContext.LoadFromStream</c> is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task LoadFromStreamReportedAsync()
        => await VerifyNet90Async(
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
    [Test]
    public async Task LoadFromNonConstantPathReportedAsync()
        => await VerifyNet90Async(
            """
            using System.Reflection;

            public class C
            {
                public Assembly M(string path) => {|SES1402:Assembly.LoadFrom(path)|};
            }
            """);

    /// <summary>Verifies <c>Assembly.LoadFile</c> with a non-constant path is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task LoadFileNonConstantPathReportedAsync()
        => await VerifyNet90Async(
            """
            using System.Reflection;

            public class C
            {
                public Assembly M(string path) => {|SES1402:Assembly.LoadFile(path)|};
            }
            """);

    /// <summary>Verifies <c>Assembly.UnsafeLoadFrom</c> with a non-constant path is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task UnsafeLoadFromNonConstantPathReportedAsync()
        => await VerifyNet90Async(
            """
            using System.Reflection;

            public class C
            {
                public Assembly M(string path) => {|SES1402:Assembly.UnsafeLoadFrom(path)|};
            }
            """);

    /// <summary>Verifies <c>Assembly.LoadFrom</c> with a string concatenation of a non-constant is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task LoadFromConcatenatedPathReportedAsync()
        => await VerifyNet90Async(
            """
            using System.Reflection;

            public class C
            {
                public Assembly M(string name) => {|SES1402:Assembly.LoadFrom("plugins/" + name + ".dll")|};
            }
            """);

    /// <summary>Verifies the safe <c>Assembly.Load(string)</c> identity overload is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task LoadByNameIsCleanAsync()
        => await VerifyNet90Async(
            """
            using System.Reflection;

            public class C
            {
                public Assembly M() => Assembly.Load("System.Text.Json");
            }
            """);

    /// <summary>Verifies the safe <c>Assembly.Load(AssemblyName)</c> identity overload is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task LoadByAssemblyNameIsCleanAsync()
        => await VerifyNet90Async(
            """
            using System.Reflection;

            public class C
            {
                public Assembly M(AssemblyName name) => Assembly.Load(name);
            }
            """);

    /// <summary>Verifies a constant literal path to <c>Assembly.LoadFrom</c> is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task LoadFromConstantPathIsCleanAsync()
        => await VerifyNet90Async(
            """
            using System.Reflection;

            public class C
            {
                public Assembly M() => Assembly.LoadFrom("plugins/known.dll");
            }
            """);

    /// <summary>Verifies a constant-field path to <c>Assembly.LoadFile</c> is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task LoadFileConstFieldPathIsCleanAsync()
        => await VerifyNet90Async(
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
    [Test]
    public async Task LoadFromStreamOfManifestResourceIsCleanAsync()
        => await VerifyNet90Async(
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
    [Test]
    public async Task UnrelatedLoadMethodIsCleanAsync()
        => await VerifyNet90Async(
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
        var test = new AnalyzeLoad.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = source
        };

        await test.RunAsync(CancellationToken.None);
    }
}
