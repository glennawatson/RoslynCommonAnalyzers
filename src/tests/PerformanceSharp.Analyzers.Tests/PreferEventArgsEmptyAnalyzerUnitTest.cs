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
    PerformanceSharp.Analyzers.Psh1022PreferEventArgsEmptyAnalyzer,
    PerformanceSharp.Analyzers.Psh1022PreferEventArgsEmptyCodeFixProvider>;

namespace PerformanceSharp.Analyzers.Tests;

/// <summary>Tests for <see cref="Psh1022PreferEventArgsEmptyAnalyzer"/> (PSH1022 use EventArgs.Empty).</summary>
public class PreferEventArgsEmptyAnalyzerUnitTest
{
    /// <summary>Verifies a parameterless allocation raised as event args is reported and replaced.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NewEventArgsIsFlaggedAndFixedAsync()
    {
        const string Source = """
                              using System;

                              public class C
                              {
                                  public event EventHandler Changed;

                                  public void Raise() => Changed?.Invoke(this, {|PSH1022:new EventArgs()|});
                              }
                              """;
        const string FixedSource = """
                                   using System;

                                   public class C
                                   {
                                       public event EventHandler Changed;

                                       public void Raise() => Changed?.Invoke(this, EventArgs.Empty);
                                   }
                                   """;
        await VerifyAsync(Source, FixedSource);
    }

    /// <summary>Verifies the target-typed <c>new()</c> form is reported and replaced with the simple name.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task TargetTypedAllocationIsFlaggedAndFixedAsync()
    {
        const string Source = """
                              using System;

                              public class C
                              {
                                  public EventArgs Make() => {|PSH1022:new()|};
                              }
                              """;
        const string FixedSource = """
                                   using System;

                                   public class C
                                   {
                                       public EventArgs Make() => EventArgs.Empty;
                                   }
                                   """;
        await VerifyAsync(Source, FixedSource);
    }

    /// <summary>Verifies a fully qualified allocation keeps the qualification the author wrote.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task QualifiedAllocationKeepsQualificationAsync()
    {
        const string Source = """
                              public class C
                              {
                                  public System.EventArgs Make() => {|PSH1022:new System.EventArgs()|};
                              }
                              """;
        const string FixedSource = """
                                   public class C
                                   {
                                       public System.EventArgs Make() => System.EventArgs.Empty;
                                   }
                                   """;
        await VerifyAsync(Source, FixedSource);
    }

    /// <summary>Verifies a derived EventArgs is never reported: it is a different type that may carry state.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task DerivedEventArgsIsCleanAsync() =>
        VerifyCleanAsync(
            """
            using System;

            public sealed class RenamedEventArgs : EventArgs
            {
            }

            public class C
            {
                public EventArgs Make() => new RenamedEventArgs();
            }
            """);

    /// <summary>Verifies a construction with an object initializer is left alone.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task InitializerAllocationIsCleanAsync() =>
        VerifyCleanAsync(
            """
            using System;

            public class C
            {
                public EventArgs Make() => new EventArgs() { };
            }
            """);

    /// <summary>Verifies another type's parameterless allocation is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task OtherTypeAllocationIsCleanAsync() =>
        VerifyCleanAsync(
            """
            public class C
            {
                public object Make() => new object();
            }
            """);

    /// <summary>Verifies namespace aliases preserve their written qualification in the replacement.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task AliasQualifiedAllocationIsFixedAsync() =>
        VerifyAsync(
            "using S = System; class C { object M() => {|PSH1022:new S::EventArgs()|}; }",
            "using S = System; class C { object M() => S::EventArgs.Empty; }");

    /// <summary>Verifies target-typed allocations require the framework type's simple name to be in scope.</summary>
    /// <param name="source">The allocation with an absent or shadowed simple name.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("class C { System.EventArgs M() => new(); }")]
    [Arguments("using System; class C { class EventArgs { } System.EventArgs M() => new(); }")]
    [Arguments("using System; class C { C M() => new(); }")]
    [Arguments("class EventArgs { } class C { object M() => new EventArgs(); }")]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task UnavailableOrDifferentSimpleNameIsCleanAsync(string source) => VerifyCleanAsync(source);

    /// <summary>Verifies constructor arguments and unresolved constructors cannot use the singleton.</summary>
    /// <param name="source">The incomplete or unsupported allocation.</param>
    /// <param name="cancellationToken">The test cancellation token.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("using System; class C { object M() => new EventArgs(1); }")]
    [Arguments("using System; class C { object M() => new EventArgs<int>(); }")]
    [Arguments("using System; class C { object M() => new EventArgs; }")]
    [Arguments("using System; class C { object M() => new EventArgs?(); }")]
    public async Task UnsupportedConstructorsAreCleanAsync(string source, CancellationToken cancellationToken)
    {
        var diagnostics = await AnalyzeAsync(source, RuntimeMetadataReferences.Platform, cancellationToken);
        await Assert.That(diagnostics).IsEmpty();
    }

    /// <summary>Verifies the rule requires an available static Empty field, not a property or instance field.</summary>
    /// <param name="member">The replacement member exposed by the framework stub.</param>
    /// <param name="reports">Whether the stub offers the required singleton field.</param>
    /// <param name="cancellationToken">The test cancellation token.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("", false)]
    [Arguments("public EventArgs Empty;", false)]
    [Arguments("public static EventArgs Empty => null;", false)]
    [Arguments("public static readonly EventArgs Empty;", true)]
    public async Task FrameworkSingletonSurfaceControlsReportingAsync(string member, bool reports, CancellationToken cancellationToken)
    {
        var source = $$"""
                       namespace System { public class EventArgs { {{member}} } }
                       class C { object M() => new System.EventArgs(); }
                       """;
        var diagnostics = await AnalyzeAsync(source, RuntimeMetadataReferences.Platform, cancellationToken);
        await Assert.That(diagnostics).Count().IsEqualTo(reports ? 1 : 0);
        if (reports)
        {
            await Assert.That(diagnostics[0].Id).IsEqualTo("PSH1022");
        }
    }

    /// <summary>Verifies a compilation without the EventArgs framework type is ignored.</summary>
    /// <param name="cancellationToken">The test cancellation token.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task MissingFrameworkTypeIsCleanAsync(CancellationToken cancellationToken)
    {
        var diagnostics = await AnalyzeAsync("class C { object M() => new EventArgs(); }", [], cancellationToken);
        await Assert.That(diagnostics).IsEmpty();
    }

    /// <summary>Runs a verification against the .NET 9 reference assemblies.</summary>
    /// <param name="source">The source with diagnostic markup.</param>
    /// <param name="fixedSource">The expected fixed source.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task VerifyAsync(string source, string fixedSource)
    {
        var test = new Verify.Test { ReferenceAssemblies = AnalyzerFrameworks.Net90, TestCode = source, FixedCode = fixedSource };

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Runs a no-diagnostic verification against the .NET 9 reference assemblies.</summary>
    /// <param name="source">The source expected to produce no diagnostics.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Task VerifyCleanAsync(string source) => VerifyAsync(source, source);

    /// <summary>Runs the analyzer on incomplete syntax or a minimal framework surface.</summary>
    /// <param name="source">The source to analyze.</param>
    /// <param name="references">Cached metadata references, or none for a missing framework.</param>
    /// <param name="cancellationToken">The test cancellation token.</param>
    /// <returns>The analyzer diagnostics.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Task<ImmutableArray<Diagnostic>> AnalyzeAsync(string source, ImmutableArray<MetadataReference> references, CancellationToken cancellationToken) =>
        CSharpCompilation.Create(
                "EventArgsConstructionTests",
                [CSharpSyntaxTree.ParseText(source, cancellationToken: cancellationToken)],
                references,
                new(OutputKind.DynamicallyLinkedLibrary))
            .WithAnalyzers([new Psh1022PreferEventArgsEmptyAnalyzer()])
            .GetAnalyzerDiagnosticsAsync(cancellationToken);
}
