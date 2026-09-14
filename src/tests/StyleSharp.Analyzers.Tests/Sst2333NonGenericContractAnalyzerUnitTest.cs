// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using VerifyContract = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<StyleSharp.Analyzers.Sst2333NonGenericContractAnalyzer>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST2333 (provide the non-generic form of a generic comparison contract).</summary>
public class Sst2333NonGenericContractAnalyzerUnitTest
{
    /// <summary>Verifies an interface inherited through a differently named contract still triggers the rule.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task InheritedComparableContractIsReportedAsync() =>
        VerifyContract.VerifyAnalyzerAsync(
            """
            public interface ISortable : System.IComparable<int> { }

            public class {|SST2333:Sortable|} : ISortable
            {
                public int CompareTo(int value) => 0;
            }
            """);

    /// <summary>Verifies a type implementing <c>IComparable&lt;T&gt;</c> without <c>IComparable</c> is reported.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task ComparableWithoutNonGenericIsReportedAsync() =>
        VerifyContract.VerifyAnalyzerAsync(
            """
            using System;

            public class {|SST2333:Money|} : IComparable<Money>
            {
                public int CompareTo(Money other) => 0;
            }
            """);

    /// <summary>Verifies a type implementing <c>IComparer&lt;T&gt;</c> without <c>IComparer</c> is reported.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task ComparerWithoutNonGenericIsReportedAsync() =>
        VerifyContract.VerifyAnalyzerAsync(
            """
            using System.Collections.Generic;

            public class {|SST2333:MoneyComparer|} : IComparer<int>
            {
                public int Compare(int x, int y) => 0;
            }
            """);

    /// <summary>Verifies a type implementing <c>IEqualityComparer&lt;T&gt;</c> without <c>IEqualityComparer</c> is reported.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task EqualityComparerWithoutNonGenericIsReportedAsync() =>
        VerifyContract.VerifyAnalyzerAsync(
            """
            using System.Collections.Generic;

            public class {|SST2333:IntEquality|} : IEqualityComparer<int>
            {
                public bool Equals(int x, int y) => true;

                public int GetHashCode(int obj) => 0;
            }
            """);

    /// <summary>Verifies a type implementing <c>IEquatable&lt;T&gt;</c> without an <c>object.Equals</c> override is reported.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task EquatableWithoutObjectEqualsIsReportedAsync() =>
        VerifyContract.VerifyAnalyzerAsync(
            """
            using System;

            public class {|SST2333:Money|} : IEquatable<Money>
            {
                public bool Equals(Money other) => true;
            }
            """);

    /// <summary>Verifies a type that also implements the non-generic contract is clean.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task ComparableWithNonGenericIsCleanAsync() =>
        VerifyContract.VerifyAnalyzerAsync(
            """
            using System;

            public class Money : IComparable<Money>, IComparable
            {
                public int CompareTo(Money other) => 0;

                public int CompareTo(object obj) => 0;
            }
            """);

    /// <summary>Verifies a type that also overrides <c>object.Equals</c> is clean.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task EquatableWithObjectEqualsIsCleanAsync() =>
        VerifyContract.VerifyAnalyzerAsync(
            """
            using System;

            public class Money : IEquatable<Money>
            {
                public bool Equals(Money other) => true;

                public override bool Equals(object obj) => obj is Money other && Equals(other);

                public override int GetHashCode() => 0;
            }
            """);

    /// <summary>Verifies a record, whose equality members the compiler already generates, is clean.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task RecordIsCleanAsync() =>
        VerifyContract.VerifyAnalyzerAsync(
            """
            public record Point(int X, int Y);
            """);

    /// <summary>Verifies an internal type, invisible outside the assembly, is not reported.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task InternalTypeIsCleanAsync() =>
        VerifyContract.VerifyAnalyzerAsync(
            """
            using System;

            internal class Money : IComparable<Money>
            {
                public int CompareTo(Money other) => 0;
            }
            """);
}
