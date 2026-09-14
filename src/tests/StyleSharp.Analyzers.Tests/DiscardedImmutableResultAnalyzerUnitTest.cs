// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis.Testing;
using RoslynCommon.Analyzers.Tests;
using VerifyDiscarded = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<StyleSharp.Analyzers.Sst2418DiscardedImmutableResultAnalyzer>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST2418 (the discarded result of an immutable value's method).</summary>
public class DiscardedImmutableResultAnalyzerUnitTest
{
    /// <summary>Verifies each stateless numeric helper and fallback immutable type reports an unused result.</summary>
    /// <param name="expression">The discarded method call.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    [Arguments("System.MathF.Abs(-1f)")]
    [Arguments("System.Convert.ToInt32(1L)")]
    [Arguments("System.HashCode.Combine(1, 2)")]
    [Arguments("System.BitConverter.GetBytes(1)")]
    [Arguments("System.TimeSpan.FromSeconds(1)")]
    [Arguments("System.Guid.NewGuid()")]
    [Arguments("decimal.Add(1m, 2m)")]
    public Task DiscardedStatelessResultsAreReportedAsync(string expression) =>
        new VerifyDiscarded.Test { ReferenceAssemblies = AnalyzerFrameworks.Net80, TestCode = $$"""class C { void M() { {|SST2418:{{expression}}|}; } }""" }
            .RunAsync(CancellationToken.None);

    /// <summary>Verifies all four span and memory result types report discarded slices.</summary>
    /// <param name="type">The slice type.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    [Arguments("System.Span<int>")]
    [Arguments("System.ReadOnlySpan<int>")]
    [Arguments("System.Memory<int>")]
    [Arguments("System.ReadOnlyMemory<int>")]
    public Task DiscardedSlicesAreReportedAsync(string type) =>
        new VerifyDiscarded.Test { ReferenceAssemblies = AnalyzerFrameworks.Net80, TestCode = $$"""class C { void M({{type}} value) { {|SST2418:value.Slice(1)|}; } }""" }
            .RunAsync(CancellationToken.None);

    /// <summary>Verifies calls already handled by another rule and calls without an immutable result stay silent.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task OtherDiagnosticOwnersAndMutableCallsAreIgnoredAsync() =>
        VerifyDiscarded.VerifyAnalyzerAsync(
            """
            using System;
            using System.Linq;
            using System.Diagnostics.Contracts;
            class C
            {
                int Changed() => 1;
                static int Create() => 1;
                [Pure] int PureValue() => 1;
                void M(int[] values)
                {
                    int value = 0;
                    value++;
                    Changed();
                    this.Changed();
                    C.Create();
                    this.PureValue();
                    int.TryParse("1", out value);
                    values.Where(item => item > 0);
                    Enumerable.Count(values);
                }
            }
            """);

    /// <summary>Verifies readonly fluent values can mutate ref and out arguments, while in arguments remain read-only.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task ReadonlySelfReturnsRespectByReferenceEffectsAsync() =>
        VerifyDiscarded.VerifyAnalyzerAsync(
            """
            using System;
            readonly struct Value
            {
                [Obsolete] public Value Copy(int value) => this;
                public Value Ref(ref int value) => this;
                public Value Out(out int value) { value = 1; return this; }
                public Value In(in int value) => this;
                public int Count() => 1;
            }
            struct Mutable { public Mutable Copy() => this; }
            class C
            {
                void M(Value value, Mutable mutable, int argument)
                {
                    {|SST2418:value.Copy(argument)|};
                    value.Ref(ref argument);
                    value.Out(out argument);
                    {|SST2418:value.In(in argument)|};
                    value.Count();
                    mutable.Copy();
                }
            }
            """);

    /// <summary>Verifies missing method and attribute bindings do not crash discarded-result analysis.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task UnresolvedCallsAndAttributesAreHandledAsync() =>
        new VerifyDiscarded.Test { TestCode = "class C { [Missing] int Value() => 1; void M() { this.Value(); missing.Call(); } }", CompilerDiagnostics = CompilerDiagnostics.None }
            .RunAsync(CancellationToken.None);

    /// <summary>Verifies absence of modern slice types still permits immutable framework results to be checked.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task FrameworkWithoutSpanTypesStillReportsImmutableValuesAsync() =>
        new VerifyDiscarded.Test { ReferenceAssemblies = AnalyzerFrameworks.NetStandard20, TestCode = "class C { void M(System.DateTime date) { {|SST2418:date.AddDays(1)|}; } }" }
            .RunAsync(CancellationToken.None);

    /// <summary>Verifies a discarded DateTime method result is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task DiscardedDateTimeResultIsReportedAsync() =>
        VerifyDiscarded.VerifyAnalyzerAsync(
            """
            using System;

            public sealed class C
            {
                public void M(DateTime date)
                {
                    {|SST2418:date.AddDays(1)|};
                }
            }
            """);

    /// <summary>Verifies a discarded static numeric-helper result is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task DiscardedMathResultIsReportedAsync() =>
        VerifyDiscarded.VerifyAnalyzerAsync(
            """
            using System;

            public sealed class C
            {
                public void M()
                {
                    {|SST2418:Math.Abs(-5)|};
                }
            }
            """);

    /// <summary>Verifies a discarded readonly-record-struct result is reported, derived from the type.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task DiscardedReadonlyRecordStructResultIsReportedAsync() =>
        VerifyDiscarded.VerifyAnalyzerAsync(
            """
            public readonly record struct Money(int Amount)
            {
                public Money Add(Money other) => new Money(Amount + other.Amount);
            }

            public sealed class C
            {
                public void M(Money money, Money other)
                {
                    {|SST2418:money.Add(other)|};
                }
            }
            """);

    /// <summary>Verifies a discarded string result is left to the unused-string diagnostic.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task DiscardedStringResultIsCleanAsync() =>
        VerifyDiscarded.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                public void M(string text)
                {
                    text.Trim();
                }
            }
            """);

    /// <summary>Verifies a void mutating method is clean.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task VoidMethodIsCleanAsync() =>
        VerifyDiscarded.VerifyAnalyzerAsync(
            """
            using System.Collections.Generic;

            public sealed class C
            {
                public void M(List<int> items)
                {
                    items.Add(1);
                }
            }
            """);

    /// <summary>Verifies a used result is clean.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task UsedResultIsCleanAsync() =>
        VerifyDiscarded.VerifyAnalyzerAsync(
            """
            using System;

            public sealed class C
            {
                public DateTime M(DateTime date) => date.AddDays(1);
            }
            """);
}
