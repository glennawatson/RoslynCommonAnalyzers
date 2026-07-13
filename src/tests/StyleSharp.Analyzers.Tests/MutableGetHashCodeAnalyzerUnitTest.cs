// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Testing;

using VerifyHash = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<StyleSharp.Analyzers.Sst1482MutableGetHashCodeAnalyzer>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST1482 (GetHashCode should not read mutable state).</summary>
public class MutableGetHashCodeAnalyzerUnitTest
{
    /// <summary>The <c>init</c>-accessor polyfill the default reference assemblies do not supply.</summary>
    private const string IsExternalInit = """

        namespace System.Runtime.CompilerServices { internal static class IsExternalInit { } }
        """;

    /// <summary>Verifies a hash that reads a field which is neither readonly nor const is reported.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task MutableFieldIsReportedAsync()
        => await VerifyHash.VerifyAnalyzerAsync(
            """
            public class C
            {
                private int _id;

                private string _name = "";

                public override int GetHashCode() => {|SST1482:_id|} ^ {|SST1482:_name|}.Length;
            }
            """);

    /// <summary>Verifies a hash built from state construction fixes is clean.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task ReadonlyConstAndStaticReadonlyAreCleanAsync()
        => await VerifyHash.VerifyAnalyzerAsync(
            """
            public class C
            {
                private const int Seed = 17;

                private static readonly int Multiplier = 31;

                private readonly int _id;

                public C(int id) => _id = id;

                public override int GetHashCode() => (Seed * Multiplier) + _id;
            }
            """);

    /// <summary>Verifies a static field that is not readonly is reported: reassigning it loses every hashed object.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task StaticMutableFieldIsReportedAsync()
        => await VerifyHash.VerifyAnalyzerAsync(
            """
            public class C
            {
                private static int _salt;

                private readonly int _id;

                public C(int id) => _id = id;

                public override int GetHashCode() => _id ^ {|SST1482:_salt|};
            }
            """);

    /// <summary>Verifies a property is judged by its setter: a settable one moves, a get-only or init-only one cannot.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <remarks>
    /// A computed get-only property is treated as fixed even when its body reads a mutable field. The rule looks at
    /// what the hash names, not at what those members go on to read.
    /// </remarks>
    [Test]
    public async Task PropertiesAreJudgedByTheirSetterAsync()
        => await VerifyHash.VerifyAnalyzerAsync(
            $$"""
            public class C
            {
                public int Settable { get; set; }

                public int PrivateSet { get; private set; }

                public int InitOnly { get; init; }

                public int GetOnly { get; }

                public int Computed => Settable + 1;

                public override int GetHashCode()
                    => {|SST1482:Settable|} ^ {|SST1482:PrivateSet|} ^ InitOnly ^ GetOnly ^ Computed;
            }{{IsExternalInit}}
            """);

    /// <summary>Verifies a local or a parameter is not state and is never reported.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task LocalsAndParametersAreCleanAsync()
        => await VerifyHash.VerifyAnalyzerAsync(
            """
            public class C
            {
                private readonly int _id;

                public C(int id) => _id = id;

                public int Mix(int seed)
                {
                    var scratch = seed;
                    return scratch;
                }

                public override int GetHashCode()
                {
                    var hash = 17;
                    hash = (hash * 31) + _id;
                    return hash;
                }
            }
            """);

    /// <summary>Verifies mutable state reached through another object is that object's business, not this one's.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task StateOnAnotherObjectIsCleanAsync()
        => await VerifyHash.VerifyAnalyzerAsync(
            """
            public class Node
            {
                public int Value { get; set; }
            }

            public class C
            {
                private readonly Node _next = new Node();

                public override int GetHashCode() => _next.Value;
            }
            """);

    /// <summary>Verifies the rule looks through <c>base.GetHashCode()</c> and <c>HashCode.Combine</c> rather than at them.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task BaseHashAndHashCodeCombineAreCleanAsync()
    {
        var test = CreateNet80Test(
            """
            public class C
            {
                private readonly int _a;

                private readonly int _b;

                public C(int a, int b)
                {
                    _a = a;
                    _b = b;
                }

                public override int GetHashCode() => System.HashCode.Combine(_a, _b) ^ base.GetHashCode();
            }
            """);

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies the arguments of <c>HashCode.Combine</c> are exactly where the mutable reads are found.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task HashCodeCombineArgumentsAreReportedAsync()
    {
        var test = CreateNet80Test(
            """
            public class C
            {
                private int _a;

                private int _b;

                public override int GetHashCode() => System.HashCode.Combine({|SST1482:_a|}, {|SST1482:_b|});
            }
            """);

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies <c>this.</c> and <c>base.</c> both still name this instance's state.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task ThisAndBaseQualifiedStateIsReportedAsync()
        => await VerifyHash.VerifyAnalyzerAsync(
            """
            public class B
            {
                protected int _shared;

                protected readonly int _fixedValue;
            }

            public class C : B
            {
                private int _own;

                public override int GetHashCode()
                    => this.{|SST1482:_own|} ^ base.{|SST1482:_shared|} ^ base._fixedValue;
            }
            """);

    /// <summary>Verifies <c>nameof</c> yields a name at compile time and never reads the field.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task NameofIsCleanAsync()
        => await VerifyHash.VerifyAnalyzerAsync(
            """
            public class C
            {
                private int _id;

                public void Renumber(int id) => _id = id;

                public override int GetHashCode() => nameof(_id).Length;
            }
            """);

    /// <summary>Verifies an object initializer names members of the object being built, not of this one.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task ObjectInitializerMemberIsCleanAsync()
        => await VerifyHash.VerifyAnalyzerAsync(
            """
            public class Box
            {
                public int Width { get; set; }
            }

            public class C
            {
                private int _size;

                public override int GetHashCode() => new Box { Width = {|SST1482:_size|} }.Width;
            }
            """);

    /// <summary>Verifies a method merely named <c>GetHashCode</c> is not the hash override and is not analyzed.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task NonOverrideGetHashCodeIsNotAnalyzedAsync()
        => await VerifyHash.VerifyAnalyzerAsync(
            """
            public class C
            {
                private int _id;

                public new int GetHashCode() => _id;

                public int GetHashCode(int seed) => _id ^ seed;
            }
            """);

    /// <summary>Verifies a member the hash reads more than once is reported once, not once per read.</summary>
    /// <remarks>
    /// One mutable member is one defect however often the hash names it. A null test followed by a use is
    /// the ordinary shape, and it must not put two squiggles on one line for a single thing to fix.
    /// </remarks>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task MemberReadTwiceIsReportedOnceAsync()
        => await VerifyHash.VerifyAnalyzerAsync(
            """
            public class C
            {
                private int[] _items = new int[1];

                public override int GetHashCode() =>
                    {|SST1482:_items|} is null ? 0 : _items.Length;
            }
            """);

    /// <summary>Builds a test that runs against the .NET 8 reference assemblies, where <c>System.HashCode</c> exists.</summary>
    /// <param name="source">The source to analyze.</param>
    /// <returns>The configured test.</returns>
    private static VerifyHash.Test CreateNet80Test(string source)
        => new()
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
            TestCode = source,
        };
}
