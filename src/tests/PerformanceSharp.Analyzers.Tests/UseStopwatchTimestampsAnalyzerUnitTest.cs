// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using RoslynCommon.Analyzers.Tests;

using Verify = PerformanceSharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    PerformanceSharp.Analyzers.Psh1408UseStopwatchTimestampsAnalyzer>;

namespace PerformanceSharp.Analyzers.Tests;

/// <summary>Tests for <see cref="Psh1408UseStopwatchTimestampsAnalyzer"/> (PSH1408 stopwatch timestamps).</summary>
public class UseStopwatchTimestampsAnalyzerUnitTest
{
    /// <summary>Verifies a stopwatch used only for elapsed reads is flagged.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task ElapsedOnlyStopwatchIsFlaggedAsync() =>
        VerifyNet90Async(
            """
            using System.Diagnostics;

            public class C
            {
                public long M()
                {
                    var stopwatch = {|PSH1408:Stopwatch.StartNew()|};
                    DoWork();
                    return stopwatch.ElapsedMilliseconds;
                }

                private static void DoWork()
                {
                }
            }
            """);

    /// <summary>Verifies a stopped stopwatch that only reads elapsed time is still flagged.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task StoppedElapsedOnlyStopwatchIsFlaggedAsync() =>
        VerifyNet90Async(
            """
            using System;
            using System.Diagnostics;

            public class C
            {
                public TimeSpan M()
                {
                    var stopwatch = {|PSH1408:Stopwatch.StartNew()|};
                    DoWork();
                    stopwatch.Stop();
                    return stopwatch.Elapsed;
                }

                private static void DoWork()
                {
                }
            }
            """);

    /// <summary>Verifies a restarted stopwatch stays clean; timestamps cannot express Restart.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task RestartedStopwatchIsCleanAsync() =>
        VerifyNet90Async(
            """
            using System.Diagnostics;

            public class C
            {
                public long M()
                {
                    var stopwatch = Stopwatch.StartNew();
                    DoWork();
                    stopwatch.Restart();
                    DoWork();
                    return stopwatch.ElapsedMilliseconds;
                }

                private static void DoWork()
                {
                }
            }
            """);

    /// <summary>Verifies a stopwatch that escapes as an argument stays clean.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task EscapingStopwatchIsCleanAsync() =>
        VerifyNet90Async(
            """
            using System.Diagnostics;

            public class C
            {
                public void M()
                {
                    var stopwatch = Stopwatch.StartNew();
                    Report(stopwatch);
                }

                private static void Report(Stopwatch stopwatch)
                {
                }
            }
            """);

    /// <summary>Verifies the rule stays silent on frameworks without GetElapsedTime.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task RuleIsGatedOnGetElapsedTimeAsync()
    {
        var test = new Verify.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net60,
            TestCode = """
                       using System.Diagnostics;

                       public class C
                       {
                           public long M()
                           {
                               var stopwatch = Stopwatch.StartNew();
                               return stopwatch.ElapsedMilliseconds;
                           }
                       }
                       """,
        };
        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies nested function and accessor bodies are searched for elapsed reads.</summary>
    /// <param name="member">The enclosing member containing the reported stopwatch.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    [Arguments("long M() { long Read() { var sw = {|PSH1408:Stopwatch.StartNew()|}; return sw.ElapsedTicks; } return Read(); }")]
    [Arguments("System.Func<long> M() => () => { var sw = {|PSH1408:Stopwatch.StartNew()|}; return sw.ElapsedTicks; };")]
    [Arguments("System.Func<long> M() => delegate { var sw = {|PSH1408:Stopwatch.StartNew()|}; return sw.ElapsedTicks; };")]
    [Arguments("long P { get { var sw = {|PSH1408:Stopwatch.StartNew()|}; return sw.ElapsedTicks; } }")]
    [Arguments("long M() { { var sw = {|PSH1408:System.Diagnostics.Stopwatch.StartNew()|}; return sw.ElapsedTicks + sw.ElapsedMilliseconds; } }")]
    public Task EnclosingBodiesAndQualifiedCallsAreReportedAsync(string member) =>
        VerifyNet90Async($"using System.Diagnostics; class C {{ {member} }}");

    /// <summary>Verifies a same-named anonymous property currently makes the syntax-only usage scan decline the suggestion.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task SameNamedAnonymousPropertyKeepsStopwatchAsync() =>
        VerifyNet90Async("using System.Diagnostics; class C { long M() { var sw = Stopwatch.StartNew(); var holder = new { sw = 1 }; return sw.ElapsedTicks; } }");

    /// <summary>Verifies unrelated declarations and calls are rejected before stopwatch binding.</summary>
    /// <param name="body">The method body containing a near-miss declaration.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    [Arguments("Stopwatch first = Stopwatch.StartNew(), second = Stopwatch.StartNew();")]
    [Arguments("Stopwatch sw;")]
    [Arguments("var sw = new Stopwatch();")]
    [Arguments("var sw = StartNew();")]
    [Arguments("var sw = Stopwatch.GetTimestamp();")]
    [Arguments("var sw = Timer.StartNew();")]
    [Arguments("var sw = Stopwatch.StartNew(1);")]
    [Arguments("var sw = (Stopwatch).StartNew();")]
    [Arguments("var sw = Stopwatch.StartNew(); sw.Stop();")]
    [Arguments("var sw = Stopwatch.StartNew(); System.Action stop = sw.Stop;")]
    [Arguments("var sw = Stopwatch.StartNew(); _ = sw.IsRunning;")]
    [Arguments("var sw = Stopwatch.StartNew(); _ = holder.sw;")]
    public Task NonmatchingDeclarationsAndUsesAreCleanAsync(string body) =>
        new Verify.Test
        {
            ReferenceAssemblies = AnalyzerFrameworks.Net90,
            CompilerDiagnostics = CompilerDiagnostics.None,
            TestCode = $"using System.Diagnostics; using Timer = System.Diagnostics.Stopwatch; using static System.Diagnostics.Stopwatch; class C {{ void M() {{ {body} }} }}",
        }.RunAsync(CancellationToken.None);

    /// <summary>Verifies declarations outside functions do not produce timestamp suggestions.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task TopLevelDeclarationIsCleanAsync() =>
        new Verify.Test
        {
            ReferenceAssemblies = AnalyzerFrameworks.Net90,
            CompilerDiagnostics = CompilerDiagnostics.None,
            TestCode = "using System.Diagnostics; var sw = Stopwatch.StartNew(); _ = sw.ElapsedTicks;",
        }.RunAsync(CancellationToken.None);

    /// <summary>Verifies unresolved and unrelated StartNew calls cannot pass the semantic gate.</summary>
    /// <param name="declaration">The candidate type or delegate with the required spelling.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    [Arguments("class Stopwatch { public static Stopwatch StartNew() => null; public long ElapsedTicks => 0; }")]
    [Arguments("class Stopwatch { public static Stopwatch StartNew(int required) => null; public long ElapsedTicks => 0; }")]
    [Arguments("class Stopwatch { public static System.Func<Stopwatch> StartNew; public long ElapsedTicks => 0; }")]
    public Task StartNewMustBindToFrameworkStopwatchAsync(string declaration) =>
        new Verify.Test
        {
            ReferenceAssemblies = AnalyzerFrameworks.Net90,
            CompilerDiagnostics = CompilerDiagnostics.None,
            TestCode = $"class C {{ long M() {{ var sw = Stopwatch.StartNew(); return sw.ElapsedTicks; }} }} {declaration}",
        }.RunAsync(CancellationToken.None);

    /// <summary>Verifies a compilation without the framework Stopwatch type is ignored.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task MissingFrameworkStopwatchIsCleanAsync()
    {
        var test = new Verify.Test
        {
            TestCode = "class C { long M() { var sw = Stopwatch.StartNew(); return sw.ElapsedTicks; } } class Stopwatch { public static Stopwatch StartNew() => null; public long ElapsedTicks => 0; }",
            CompilerDiagnostics = CompilerDiagnostics.None,
        };
        test.SolutionTransforms.Add(static (solution, projectId) =>
            solution.WithProjectMetadataReferences(projectId, ImmutableArray<MetadataReference>.Empty));
        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Runs a verification against the .NET 9 reference assemblies.</summary>
    /// <param name="source">The test source.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task VerifyNet90Async(string source)
    {
        var test = new Verify.Test { ReferenceAssemblies = ReferenceAssemblies.Net.Net90, TestCode = source, };
        await test.RunAsync(CancellationToken.None);
    }
}
