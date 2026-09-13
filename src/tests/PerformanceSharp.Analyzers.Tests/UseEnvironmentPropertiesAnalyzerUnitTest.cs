// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Testing;
using RoslynCommon.Analyzers.Tests;

using Verify = PerformanceSharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    PerformanceSharp.Analyzers.Psh1405UseEnvironmentPropertiesAnalyzer>;

namespace PerformanceSharp.Analyzers.Tests;

/// <summary>Tests the syntax, binding, and replacement API gates for PSH1405.</summary>
public class UseEnvironmentPropertiesAnalyzerUnitTest
{
    /// <summary>Primitive definitions for compilations whose framework intentionally omits runtime APIs.</summary>
    private const string PrimitiveTypes = """
        namespace System
        {
            public class Object { }
            public class ValueType { }
            public struct Void { }
            public struct Boolean { }
            public struct Int32 { }
            public class String { }
        }
        """;

    /// <summary>The original APIs, independently of which Environment replacements exist.</summary>
    private const string OriginalApis = """
        namespace System.Diagnostics
        {
            public class Process
            {
                public static Process GetCurrentProcess() => null;
                public int Id => 0;
                public ProcessModule MainModule => null;
            }
            public class ProcessModule { public string FileName => null; }
        }
        namespace System.Threading
        {
            public class Thread
            {
                public static Thread CurrentThread => null;
                public int ManagedThreadId => 0;
            }
        }
        """;

    /// <summary>Verifies all three chains, qualification, aliases, and using-static factories.</summary>
    /// <param name="expression">The chain that should be replaced.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("Process.GetCurrentProcess().Id")]
    [Arguments("Process.GetCurrentProcess().MainModule.FileName")]
    [Arguments("Thread.CurrentThread.ManagedThreadId")]
    [Arguments("System.Diagnostics.Process.GetCurrentProcess().Id")]
    [Arguments("P.GetCurrentProcess().MainModule.FileName")]
    [Arguments("GetCurrentProcess().Id")]
    [Arguments("GetCurrentProcess().MainModule.FileName")]
    public async Task CurrentRuntimeChainIsReportedAsync(string expression)
    {
        var source = $$"""
            using System.Diagnostics;
            using System.Threading;
            using P = System.Diagnostics.Process;
            using static System.Diagnostics.Process;
            public class C { public object M() => {|PSH1405:{{expression}}|}; }
            """;
        await VerifyAsync(source);
    }

    /// <summary>Verifies calls that differ in one syntactic component are rejected by the shared recognizer.</summary>
    /// <param name="expression">The candidate member access.</param>
    /// <param name="replacement">The expected property, or an empty string for a near-miss.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("Process.GetCurrentProcess().Id", "ProcessId")]
    [Arguments("GetCurrentProcess().Id", "ProcessId")]
    [Arguments("Process.GetCurrentProcess().MainModule.FileName", "ProcessPath")]
    [Arguments("Thread.CurrentThread.ManagedThreadId", "CurrentManagedThreadId")]
    [Arguments("process.Name", "")]
    [Arguments("process.Id", "")]
    [Arguments("Process.Other().Id", "")]
    [Arguments("Other().Id", "")]
    [Arguments("Process.GetCurrentProcess(1).Id", "")]
    [Arguments("factories[0]().Id", "")]
    [Arguments("GetCurrentProcess<int>().Id", "")]
    [Arguments("module.FileName", "")]
    [Arguments("GetModule().FileName", "")]
    [Arguments("process.Other.FileName", "")]
    [Arguments("process.MainModule.FileName", "")]
    [Arguments("Process.Other().MainModule.FileName", "")]
    [Arguments("thread.ManagedThreadId", "")]
    [Arguments("GetThread().ManagedThreadId", "")]
    [Arguments("Thread.Other.ManagedThreadId", "")]
    public async Task ReplacementShapeIsClassifiedAsync(string expression, string replacement)
    {
        var access = (MemberAccessExpressionSyntax)SyntaxFactory.ParseExpression(expression);
        var matches = Psh1405UseEnvironmentPropertiesAnalyzer.TryGetReplacementPropertyName(access, out var actual);
        await Assert.That(matches).IsEqualTo(replacement.Length != 0);
        await Assert.That(actual).IsEqualTo(replacement);
    }

    /// <summary>Verifies already direct properties and chains on saved objects remain silent.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task DirectApisAndSavedObjectsAreCleanAsync()
    {
        const string Source = """
            using System;
            using System.Diagnostics;
            using System.Threading;
            public class C
            {
                public object[] M(Process process, Thread thread) => new object[]
                {
                    process.Id, process.MainModule.FileName, thread.ManagedThreadId,
                    Environment.ProcessId, Environment.ProcessPath, Environment.CurrentManagedThreadId,
                    Process.GetProcesses()[0].Id, Process.GetCurrentProcess().ProcessName,
                    Process.GetCurrentProcess()?.Id
                };
            }
            """;
        await VerifyAsync(Source);
    }

    /// <summary>Verifies older real reference assemblies disable only the unavailable process replacements.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task FrameworkWithoutProcessReplacementsIsCleanAsync()
    {
        const string Source = """
            using System.Diagnostics;
            using System.Threading;
            public class C
            {
                public int Id() => Process.GetCurrentProcess().Id;
                public string Path() => Process.GetCurrentProcess().MainModule.FileName;
                public int ThreadId() => {|PSH1405:Thread.CurrentThread.ManagedThreadId|};
            }
            """;
        await VerifyAsync(Source, AnalyzerFrameworks.Net462);
    }

    /// <summary>Verifies foreign factories and thread properties never pass the declaring-type and member-kind gates.</summary>
    /// <param name="member">The member declared on the foreign type.</param>
    /// <param name="expression">The otherwise matching call chain.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("public static Other GetCurrentProcess() => null;", "Other.GetCurrentProcess().Id")]
    [Arguments("public Other GetCurrentProcess() => null;", "new Other().GetCurrentProcess().Id")]
    [Arguments("public static Other GetCurrentProcess(int ignored = 0) => null;", "Other.GetCurrentProcess().Id")]
    [Arguments("public static System.Func<Other> GetCurrentProcess => () => null;", "Other.GetCurrentProcess().Id")]
    [Arguments("public static Other GetCurrentProcess() => null;", "Other.GetCurrentProcess().MainModule.FileName")]
    [Arguments("public static Other CurrentThread => null;", "Other.CurrentThread.ManagedThreadId")]
    [Arguments("public Other CurrentThread => null;", "new Other().CurrentThread.ManagedThreadId")]
    [Arguments("public static Other CurrentThread;", "Other.CurrentThread.ManagedThreadId")]
    public async Task ForeignChainIsCleanAsync(string member, string expression)
    {
        var source = $$"""
            public class Other
            {
                {{member}}
                public int Id => 0;
                public int ManagedThreadId => 0;
                public Other MainModule => null;
                public string FileName => null;
            }
            public class C { public object M() => {{expression}}; }
            """;
        await VerifyAsync(source);
    }

    /// <summary>Verifies unresolved factories and properties produce only their compiler diagnostics.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task UnresolvedChainIsCleanAsync()
    {
        const string Source = """
            using System.Diagnostics;
            using System.Threading;
            public class C
            {
                public object M() => {|CS0103:GetCurrentProcess|}().Id;
                public object N() => {|CS0103:Missing|}.CurrentThread.ManagedThreadId;
            }
            """;
        await VerifyAsync(Source);
    }

    /// <summary>Verifies missing, non-property, and instance replacements do not enable diagnostics.</summary>
    /// <param name="environment">The intentionally incomplete Environment surface.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("")]
    [Arguments("namespace System { public class Environment { } }")]
    [Arguments("namespace System { public class Environment { public static int ProcessId; public static string ProcessPath; public static int CurrentManagedThreadId; } }")]
    [Arguments("namespace System { public class Environment { public int ProcessId => 0; public string ProcessPath => null; public int CurrentManagedThreadId => 0; } }")]
    public async Task MissingReplacementApiIsCleanAsync(string environment)
    {
        var source = $$"""
            {{environment}}
            {{OriginalApis}}
            public class C
            {
                public int Id() => System.Diagnostics.Process.GetCurrentProcess().Id;
                public string Path() => System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName;
                public int ThreadId() => System.Threading.Thread.CurrentThread.ManagedThreadId;
            }
            """;
        await VerifyFrameworkAsync(source);
    }

    /// <summary>Verifies replacement properties alone cannot enable a rule whose original framework types are absent.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task MissingOriginalFrameworkTypesIsCleanAsync()
    {
        const string Source = """
            namespace System
            {
                public class Environment
                {
                    public static int ProcessId => 0;
                    public static string ProcessPath => null;
                    public static int CurrentManagedThreadId => 0;
                }
            }
            public class Other
            {
                public static Other GetCurrentProcess() => null;
                public static Other CurrentThread => null;
                public int Id => 0;
                public int ManagedThreadId => 0;
                public Other MainModule => null;
                public string FileName => null;
                public int M() => Other.GetCurrentProcess().Id;
                public string N() => Other.GetCurrentProcess().MainModule.FileName;
                public int P() => Other.CurrentThread.ManagedThreadId;
            }
            """;
        await VerifyFrameworkAsync(Source);
    }

    /// <summary>Runs a marked-source test with a cached framework reference set.</summary>
    /// <param name="source">The source and expected diagnostics.</param>
    /// <param name="framework">The target framework, defaulting to .NET 9.</param>
    /// <returns>A task representing the asynchronous verification.</returns>
    private static async Task VerifyAsync(string source, ReferenceAssemblies? framework = null)
    {
        var test = new Verify.Test { ReferenceAssemblies = framework ?? AnalyzerFrameworks.Net90, TestCode = source };
        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Runs against source-defined framework primitives so omitted types are truly unavailable.</summary>
    /// <param name="source">The framework surface and consumer with expected diagnostics.</param>
    /// <returns>A task representing the asynchronous verification.</returns>
    private static async Task VerifyFrameworkAsync(string source)
    {
        var test = new Verify.Test { ReferenceAssemblies = AnalyzerFrameworks.Net90, TestCode = source };
        test.TestState.Sources.Add(PrimitiveTypes);
        test.SolutionTransforms.Add(static (solution, projectId) => solution.WithProjectMetadataReferences(projectId, []));
        await test.RunAsync(CancellationToken.None);
    }
}
