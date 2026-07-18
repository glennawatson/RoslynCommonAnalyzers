// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Testing;

using Verify = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<StyleSharp.Analyzers.Sst2464EqualityOperatorOnMutableClassAnalyzer>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST2464 (a value-equality operator declared on a mutable reference type).</summary>
public class EqualityOperatorOnMutableClassAnalyzerUnitTest
{
    /// <summary>Verifies a class with a settable field and an <c>operator ==</c> is reported once, on the operator.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SettableFieldIsReportedAsync()
        => await VerifyReportAsync(
            """
            public class C
            {
                public int X;
                public static bool operator {|SST2464:==|}(C a, C b) => false;
                public static bool operator !=(C a, C b) => true;
            }
            """);

    /// <summary>Verifies a class made mutable only by a settable property is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SettablePropertyIsReportedAsync()
        => await VerifyReportAsync(
            """
            public class C
            {
                public int X { get; private set; }
                public static bool operator {|SST2464:==|}(C a, C b) => false;
                public static bool operator !=(C a, C b) => true;
            }
            """);

    /// <summary>Verifies mutable state declared in a different partial part than the operator is still seen after binding.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task MutableStateInOtherPartialIsReportedAsync()
        => await VerifyReportAsync(
            """
            public partial class C
            {
                public static bool operator {|SST2464:==|}(C a, C b) => false;
                public static bool operator !=(C a, C b) => true;
            }

            public partial class C
            {
                public int X;
            }
            """);

    /// <summary>Verifies an immutable class — readonly fields, get-only properties — with an <c>operator ==</c> is silent.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ImmutableClassIsCleanAsync()
        => await VerifyCleanAsync(
            """
            public class C
            {
                private readonly int _x;
                public int Y => _x;
                public static bool operator ==(C a, C b) => false;
                public static bool operator !=(C a, C b) => true;
            }
            """);

    /// <summary>Verifies a class whose only settable-looking member is an init-only property is silent.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task InitOnlyPropertyIsCleanAsync()
        => await VerifyCleanAsync(
            """
            public class C
            {
                public int X { get; init; }
                public static bool operator ==(C a, C b) => false;
                public static bool operator !=(C a, C b) => true;
            }
            """);

    /// <summary>Verifies a class whose mutable-looking members are all static is silent.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task StaticStateIsCleanAsync()
        => await VerifyCleanAsync(
            """
            public class C
            {
                public static int Shared;
                public const int Limit = 3;
                public static bool operator ==(C a, C b) => false;
                public static bool operator !=(C a, C b) => true;
            }
            """);

    /// <summary>Verifies a mutable struct with an <c>operator ==</c> is silent — a value type is copied, not keyed by reference.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task StructIsCleanAsync()
        => await VerifyCleanAsync(
            """
            public struct S
            {
                public int X;
                public static bool operator ==(S a, S b) => false;
                public static bool operator !=(S a, S b) => true;
            }
            """);

    /// <summary>Verifies a record with settable state is silent — records own their value equality by design.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task RecordIsCleanAsync()
        => await VerifyCleanAsync(
            """
            public record R
            {
                public int X { get; set; }
            }
            """);

    /// <summary>Verifies a record struct with settable state is silent.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task RecordStructIsCleanAsync()
        => await VerifyCleanAsync(
            """
            public record struct R
            {
                public int X { get; set; }
            }
            """);

    /// <summary>Verifies an operator other than <c>==</c> on a mutable class is never reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NonEqualityOperatorIsCleanAsync()
        => await VerifyCleanAsync(
            """
            public class C
            {
                public int X;
                public static C operator +(C a, C b) => a;
            }
            """);

    /// <summary>Runs a report verification against the .NET 9 reference assemblies.</summary>
    /// <param name="source">The source with diagnostic markup.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task VerifyReportAsync(string source)
    {
        var test = new Verify.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = source,
        };

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Runs a no-diagnostic verification against the .NET 9 reference assemblies.</summary>
    /// <param name="source">The source expected to produce no diagnostics.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task VerifyCleanAsync(string source) => await VerifyReportAsync(source);
}
