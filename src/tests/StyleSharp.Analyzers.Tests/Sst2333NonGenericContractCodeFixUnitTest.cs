// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyContract = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.Sst2333NonGenericContractAnalyzer,
    StyleSharp.Analyzers.Sst2333NonGenericContractCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Tests for <see cref="Sst2333NonGenericContractCodeFixProvider"/> (SST2333 add the non-generic member).</summary>
public class Sst2333NonGenericContractCodeFixUnitTest
{
    /// <summary>A type implementing only the generic comparable contract.</summary>
    private const string ComparableSource = """
        using System;

        public class {|SST2333:Money|} : IComparable<Money>
        {
            public int CompareTo(Money other) => 0;
        }
        """;

    /// <summary>The type after the fix adds the non-generic comparable contract.</summary>
    private const string ComparableFixed = """
        using System;

        public class Money : IComparable<Money>, IComparable
        {
            public int CompareTo(Money other) => 0;

            int IComparable.CompareTo(object obj) => obj is Money other ? ((IComparable<Money>)this).CompareTo(other) : throw new InvalidCastException();
        }
        """;

    /// <summary>A type implementing only the generic comparer contract.</summary>
    private const string ComparerSource = """
        using System.Collections.Generic;

        public class {|SST2333:IntComparer|} : IComparer<int>
        {
            public int Compare(int x, int y) => 0;
        }
        """;

    /// <summary>The type after the fix adds the non-generic comparer contract.</summary>
    private const string ComparerFixed = """
        using System.Collections.Generic;

        public class IntComparer : IComparer<int>, System.Collections.IComparer
        {
            public int Compare(int x, int y) => 0;

            int System.Collections.IComparer.Compare(object x, object y) => ((IComparer<int>)this).Compare((int)x, (int)y);
        }
        """;

    /// <summary>A type implementing only the generic equality-comparer contract.</summary>
    private const string EqualityComparerSource = """
        using System.Collections.Generic;

        public class {|SST2333:IntEquality|} : IEqualityComparer<int>
        {
            public bool Equals(int x, int y) => true;

            public int GetHashCode(int obj) => 0;
        }
        """;

    /// <summary>The type after the fix adds the non-generic equality-comparer contract.</summary>
    private const string EqualityComparerFixed = """
        using System.Collections.Generic;

        public class IntEquality : IEqualityComparer<int>, System.Collections.IEqualityComparer
        {
            public bool Equals(int x, int y) => true;

            public int GetHashCode(int obj) => 0;

            bool System.Collections.IEqualityComparer.Equals(object x, object y) => ((IEqualityComparer<int>)this).Equals((int)x, (int)y);
            int System.Collections.IEqualityComparer.GetHashCode(object obj) => ((IEqualityComparer<int>)this).GetHashCode((int)obj);
        }
        """;

    /// <summary>A type implementing only the generic equatable contract.</summary>
    private const string EquatableSource = """
        using System;

        public class {|SST2333:Money|} : IEquatable<Money>
        {
            public bool Equals(Money other) => true;

            public override int GetHashCode() => 0;
        }
        """;

    /// <summary>The type after the fix adds an <c>object.Equals</c> override.</summary>
    private const string EquatableFixed = """
        using System;

        public class Money : IEquatable<Money>
        {
            public bool Equals(Money other) => true;

            public override int GetHashCode() => 0;

            public override bool Equals(object obj) => obj is Money other && ((IEquatable<Money>)this).Equals(other);
        }
        """;

    /// <summary>Verifies the fix adds <c>IComparable</c> forwarding to <c>IComparable&lt;T&gt;</c>.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task AddsNonGenericComparableAsync()
        => await VerifyContract.VerifyCodeFixAsync(ComparableSource, ComparableFixed);

    /// <summary>Verifies the fix adds <c>IComparer</c> forwarding to <c>IComparer&lt;T&gt;</c>.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task AddsNonGenericComparerAsync()
        => await VerifyContract.VerifyCodeFixAsync(ComparerSource, ComparerFixed);

    /// <summary>Verifies the fix adds <c>IEqualityComparer</c> forwarding to <c>IEqualityComparer&lt;T&gt;</c>.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task AddsNonGenericEqualityComparerAsync()
        => await VerifyContract.VerifyCodeFixAsync(EqualityComparerSource, EqualityComparerFixed);

    /// <summary>Verifies the fix adds an <c>object.Equals</c> override forwarding to <c>IEquatable&lt;T&gt;</c>.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task AddsObjectEqualsOverrideAsync()
        => await VerifyContract.VerifyCodeFixAsync(EquatableSource, EquatableFixed);
}
