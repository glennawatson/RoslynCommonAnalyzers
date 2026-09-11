// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using Verify = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    StyleSharp.Analyzers.Sst2463InheritedFieldCaseClashAnalyzer>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Tests for SST2463 (a derived field differing from an inherited field only by case).</summary>
public class InheritedFieldCaseClashAnalyzerUnitTest
{
    /// <summary>Verifies a derived field that case-clashes with a protected inherited field is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task ProtectedInheritedCaseClashIsReportedAsync() =>
        Verify.VerifyAnalyzerAsync(
            """
            public class Base
            {
                protected int _value;
            }

            public class Derived : Base
            {
                private int {|SST2463:_Value|};

                public void Use() => _Value = 1;
            }
            """);

    /// <summary>Verifies a public inherited case-clash is reported (the shape the framework's same-scope rule misses).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task PublicInheritedCaseClashIsReportedAsync() =>
        Verify.VerifyAnalyzerAsync(
            """
            public class Base
            {
                public int Value;
            }

            public class Derived : Base
            {
                public int {|SST2463:VALUE|};
            }
            """);

    /// <summary>Verifies an internal inherited case-clash is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task InternalInheritedCaseClashIsReportedAsync() =>
        Verify.VerifyAnalyzerAsync(
            """
            public class Base
            {
                internal int count;
            }

            public class Derived : Base
            {
                internal int {|SST2463:Count|};
            }
            """);

    /// <summary>Verifies a case-clash with a field two levels up the base chain is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task GrandparentInheritedCaseClashIsReportedAsync() =>
        Verify.VerifyAnalyzerAsync(
            """
            public class Root
            {
                protected int _total;
            }

            public class Middle : Root
            {
            }

            public class Leaf : Middle
            {
                private int {|SST2463:_Total|};

                public void Use() => _Total = 1;
            }
            """);

    /// <summary>Verifies a private base field, invisible to the derived type, is never matched.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task PrivateBaseFieldIsCleanAsync() =>
        Verify.VerifyAnalyzerAsync(
            """
            public class Base
            {
                private int _hidden;

                public int Read() => _hidden;
            }

            public class Derived : Base
            {
                private int _Hidden;

                public void Use() => _Hidden = 1;
            }
            """);

    /// <summary>Verifies an exactly matching inherited name (deliberate hiding) is never reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task IdenticalInheritedNameIsCleanAsync() =>
        Verify.VerifyAnalyzerAsync(
            """
            public class Base
            {
                protected int _same;
            }

            public class Derived : Base
            {
                private new int _same;

                public void Use() => _same = 1;
            }
            """);

    /// <summary>Verifies names that differ by more than case are two ordinary fields and are not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task NamesDifferingByMoreThanCaseAreCleanAsync() =>
        Verify.VerifyAnalyzerAsync(
            """
            public class Base
            {
                protected int _value;
            }

            public class Derived : Base
            {
                private int _values;

                public void Use() => _values = 1;
            }
            """);

    /// <summary>Verifies two fields case-clashing within one type (no inheritance) are not this rule's concern.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task SameTypeCaseClashIsCleanAsync() =>
        Verify.VerifyAnalyzerAsync(
            """
            public class C
            {
                private int _value;
                private int _Value;

                public void Use() { _value = 1; _Value = 2; }
            }
            """);

    /// <summary>Verifies a static base field is not matched against a derived instance field.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task StaticBaseFieldIsCleanAsync() =>
        Verify.VerifyAnalyzerAsync(
            """
            public class Base
            {
                protected static int _value;
            }

            public class Derived : Base
            {
                private int _Value;

                public void Use() => _Value = 1;
            }
            """);

    /// <summary>Verifies a derived static field is not reported even when it case-clashes with an inherited instance field.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task DerivedStaticFieldIsCleanAsync() =>
        Verify.VerifyAnalyzerAsync(
            """
            public class Base
            {
                protected int _value;
            }

            public class Derived : Base
            {
                private static int _Value;

                public static void Use() => _Value = 1;
            }
            """);

    /// <summary>Verifies an inherited auto-property's implicit backing field never produces a false positive.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task InheritedAutoPropertyBackingFieldIsCleanAsync() =>
        Verify.VerifyAnalyzerAsync(
            """
            public class Base
            {
                public int Value { get; set; }
            }

            public class Derived : Base
            {
                private int value;

                public void Use() => value = 1;
            }
            """);

    /// <summary>Verifies a class deriving directly from object is not inspected.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task ClassDerivingFromObjectIsCleanAsync() =>
        Verify.VerifyAnalyzerAsync(
            """
            public class C
            {
                private int _value;

                public void Use() => _value = 1;
            }
            """);
}
