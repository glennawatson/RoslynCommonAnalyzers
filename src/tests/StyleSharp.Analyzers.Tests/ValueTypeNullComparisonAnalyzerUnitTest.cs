// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyValueTypeNullComparison = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    StyleSharp.Analyzers.Sst1469ValueTypeNullComparisonAnalyzer>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for <see cref="Sst1469ValueTypeNullComparisonAnalyzer"/>.</summary>
public class ValueTypeNullComparisonAnalyzerUnitTest
{
    /// <summary>Verifies an int equality comparison against null is reported as always false.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task IntEqualsNullIsReportedAsync()
        => await VerifyValueTypeNullComparison.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                public bool M(int value) => {|#0:value == null|};
            }
            """,
            VerifyValueTypeNullComparison.Diagnostic().WithLocation(0).WithArguments("int", "false"));

    /// <summary>Verifies a DateTime inequality comparison against null is reported as always true.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task DateTimeNotEqualsNullIsReportedAsync()
        => await VerifyValueTypeNullComparison.VerifyAnalyzerAsync(
            """
            using System;

            public sealed class C
            {
                public bool M(DateTime value) => {|#0:value != null|};
            }
            """,
            VerifyValueTypeNullComparison.Diagnostic().WithLocation(0).WithArguments("DateTime", "true"));

    /// <summary>Verifies an enum compared to null is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task EnumComparedToNullIsReportedAsync()
        => await VerifyValueTypeNullComparison.VerifyAnalyzerAsync(
            """
            public enum Color
            {
                Red,
                Green
            }

            public sealed class C
            {
                public bool M(Color value) => {|SST1469:value == null|};
            }
            """);

    /// <summary>Verifies a user struct with a user-defined equality operator compared to null is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task UserStructWithEqualityOperatorIsReportedAsync()
        => await VerifyValueTypeNullComparison.VerifyAnalyzerAsync(
            """
            public struct Money
            {
                public static bool operator ==(Money left, Money right) => true;

                public static bool operator !=(Money left, Money right) => false;
            }

            public sealed class C
            {
                public bool M(Money value) => {|SST1469:value == null|};
            }
            """);

    /// <summary>Verifies a null literal on the left side of the comparison is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NullOnLeftSideIsReportedAsync()
        => await VerifyValueTypeNullComparison.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                public bool M(int value) => {|SST1469:null != value|};
            }
            """);

    /// <summary>Verifies a nullable value type compared to null is clean.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NullableValueTypeIsCleanAsync()
        => await VerifyValueTypeNullComparison.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                public bool M(int? value) => value == null;
            }
            """);

    /// <summary>Verifies a string compared to null is clean.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task StringComparedToNullIsCleanAsync()
        => await VerifyValueTypeNullComparison.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                public bool M(string value) => value == null;
            }
            """);

    /// <summary>Verifies an object compared to null is clean.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task DefaultObjectComparedToNullIsCleanAsync()
        => await VerifyValueTypeNullComparison.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                public bool M() => default(object) == null;
            }
            """);
}
