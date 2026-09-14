// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis.Testing;

using Verify = PerformanceSharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    PerformanceSharp.Analyzers.Psh1417ExpensiveDebugAssertArgumentAnalyzer>;

namespace PerformanceSharp.Analyzers.Tests;

/// <summary>Tests for <see cref="Psh1417ExpensiveDebugAssertArgumentAnalyzer"/> (PSH1417 expensive assertion arguments).</summary>
public class ExpensiveDebugAssertArgumentAnalyzerUnitTest
{
    /// <summary>Verifies the missing Debug framework type is cached without reporting unresolved assertions.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task MissingDebugFrameworkTypeIsCleanAsync()
    {
        var tree = Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree.ParseText("class C { void M() { Debug.Assert(true, Read()); Debug.Assert(true, Read()); } }");
        var compilation = Microsoft.CodeAnalysis.CSharp.CSharpCompilation.Create("MissingDebug", [tree]);
        var analyzers = Microsoft.CodeAnalysis.Diagnostics.DiagnosticAnalyzerExtensions.WithAnalyzers(compilation, [new Psh1417ExpensiveDebugAssertArgumentAnalyzer()]);
        await Assert.That(await analyzers.GetAnalyzerDiagnosticsAsync()).IsEmpty();
    }

    /// <summary>Verifies subtree scanning distinguishes nested calls from cheap reads.</summary>
    /// <param name="message">The assertion message with expected diagnostic markup.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    [Arguments("{|PSH1417:\"value: \" + value.ToString()|}")]
    [Arguments("{|PSH1417:flag ? value.ToString() : \"empty\"|}")]
    [Arguments("flag ? text : \"empty\"")]
    [Arguments("{|PSH1417:new string('x', 2)|}")]
    [Arguments("{|PSH1417:new('x', 2)|}")]
    [Arguments("$\"\"")]
    public Task QualifiedAssertScansMessageExpressionsAsync(string message) =>
        VerifyAsync($$"""class C { void M(bool flag, int value, string text) => System.Diagnostics.Debug.Assert(flag, {{message}}); }""");

    /// <summary>Verifies interpolation is expensive when the framework has no deferred handler overload.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task OlderFrameworkReportsEagerInterpolationAsync() =>
        new Verify.Test
        {
            ReferenceAssemblies = RoslynCommon.Analyzers.Tests.AnalyzerFrameworks.NetStandard20,
            TestCode = """class C { void M(int value) => System.Diagnostics.Debug.Assert(true, {|PSH1417:$"{value}"|}, {|PSH1417:$"{value.ToString()}"|}); }""",
        }.RunAsync(CancellationToken.None);

    /// <summary>Verifies unresolved, instance, and lookalike assertion targets are not reported.</summary>
    /// <param name="source">The near-miss invocation.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    [Arguments("class C { void M() => Debug.Assert(true, Get()); string Get() => null; }")]
    [Arguments("class Debug { public static void Assert(bool value, string text) { } } class C { void M() => Debug.Assert(true, Get()); string Get() => null; }")]
    [Arguments("class D { public void Assert(bool value, string text) { } } class C { void M(D Debug) => Debug.Assert(true, Get()); string Get() => null; }")]
    [Arguments("class C { void M() => System.Diagnostics.Debug.Assert(); }")]
    [Arguments("class C { void M() => System.Diagnostics.Debug.WriteLine(Get()); string Get() => null; }")]
    [Arguments("class C { void M() => (Debug).Assert(true, Get()); string Get() => null; }")]
    [Arguments("using static System.Diagnostics.Debug; class C { void M() => Assert(true, Get()); string Get() => null; }")]
    public Task NonmatchingAssertionTargetsAreCleanAsync(string source) =>
        new Verify.Test { TestCode = source, CompilerDiagnostics = CompilerDiagnostics.None }.RunAsync(CancellationToken.None);

    /// <summary>Verifies a call in the assertion condition is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    /// <remarks>
    /// A release build compiles the whole call away, arguments included, so nothing is paid there; a debug
    /// build has to evaluate the condition for the assertion to mean anything. Either way the condition is
    /// not work that gets thrown away. Reported as issue #52.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task CallInConditionIsNotReportedAsync() =>
        VerifyAsync(
            """
            using System.Collections.Generic;
            using System.Diagnostics;
            using System.Linq;

            public class C
            {
                public void M(IEnumerable<int> source)
                    => Debug.Assert(source.Any(x => x > 0));
            }
            """);

    /// <summary>Verifies an interpolated message is not reported where the framework defers it.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    /// <remarks>
    /// The interpolated-string overload hands the framework a handler that only builds the message once the
    /// condition has already failed, so a passing assertion pays nothing. It is the shape to move towards.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task InterpolatedMessageIsNotReportedAsync() =>
        VerifyAsync(
            """
            using System.Diagnostics;

            public class C
            {
                public void M(object value)
                    => Debug.Assert(value != null, $"value was {value.ToString()}");
            }
            """);

    /// <summary>Verifies a message built by a call is reported, because a passing assertion still builds it.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task CallInMessageIsReportedAsync() =>
        VerifyAsync(
            """
            using System.Collections.Generic;
            using System.Diagnostics;

            public class C
            {
                public void M(List<string> items)
                    => Debug.Assert(items.Count > 0, {|PSH1417:string.Join(",", items)|});
            }
            """);

    /// <summary>Verifies a cheap comparison is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task CheapConditionIsNotReportedAsync() =>
        VerifyAsync(
            """
            using System.Diagnostics;

            public class C
            {
                public void M(object value) => Debug.Assert(value != null);
            }
            """);

    /// <summary>Verifies a property read is not reported, because it costs nothing worth moving.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task PropertyReadIsNotReportedAsync() =>
        VerifyAsync(
            """
            using System.Collections.Generic;
            using System.Diagnostics;

            public class C
            {
                public void M(List<int> values) => Debug.Assert(values.Count > 0);
            }
            """);

    /// <summary>Verifies a constant message is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task ConstantMessageIsNotReportedAsync() =>
        VerifyAsync(
            """
            using System.Diagnostics;

            public class C
            {
                public void M(object value) => Debug.Assert(value != null, "value must not be null");
            }
            """);

    /// <summary>Verifies nameof is not reported: it is an invocation in syntax only, and folds to a constant.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task NameOfMessageIsNotReportedAsync() =>
        VerifyAsync(
            """
            using System.Diagnostics;

            public class C
            {
                public void M(object value) => Debug.Assert(value != null, nameof(value));
            }
            """);

    /// <summary>Runs an analyzer verification against the .NET 9 reference assemblies.</summary>
    /// <param name="source">The source with diagnostic markup.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task VerifyAsync(string source)
    {
        var test = new Verify.Test { ReferenceAssemblies = ReferenceAssemblies.Net.Net90, TestCode = source };

        await test.RunAsync(CancellationToken.None);
    }
}
