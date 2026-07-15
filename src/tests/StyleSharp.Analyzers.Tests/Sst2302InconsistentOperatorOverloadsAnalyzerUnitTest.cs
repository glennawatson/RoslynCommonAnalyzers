// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyOperators = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<StyleSharp.Analyzers.Sst2302InconsistentOperatorOverloadsAnalyzer>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST2302 (overload operators in their complete set).</summary>
public class Sst2302InconsistentOperatorOverloadsAnalyzerUnitTest
{
    /// <summary>Verifies <c>==</c> without either equality override is reported once, on the operator.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task EqualityOperatorWithoutEqualityOverridesIsReportedAsync()
        => await VerifyOperators.VerifyAnalyzerAsync(
            """
            public class Money
            {
                public static bool operator {|SST2302:==|}(Money left, Money right) => true;

                public static bool operator !=(Money left, Money right) => false;
            }
            """);

    /// <summary>Verifies <c>==</c> with an <c>Equals(object)</c> override but no hash is still reported.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task EqualityOperatorWithoutHashCodeIsReportedAsync()
        => await VerifyOperators.VerifyAnalyzerAsync(
            """
            public class Money
            {
                public static bool operator {|SST2302:==|}(Money left, Money right) => true;

                public static bool operator !=(Money left, Money right) => false;

                public override bool Equals(object obj) => true;
            }
            """);

    /// <summary>Verifies the complete equality set is clean.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task CompleteEqualitySetIsCleanAsync()
        => await VerifyOperators.VerifyAnalyzerAsync(
            """
            public class Money
            {
                public static bool operator ==(Money left, Money right) => true;

                public static bool operator !=(Money left, Money right) => false;

                public override bool Equals(object obj) => true;

                public override int GetHashCode() => 0;
            }
            """);

    /// <summary>Verifies an inherited equality override does not answer for a type that adds its own <c>==</c>.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task InheritedEqualityOverrideDoesNotCountAsync()
        => await VerifyOperators.VerifyAnalyzerAsync(
            """
            public class Amount
            {
                public override bool Equals(object obj) => true;

                public override int GetHashCode() => 0;
            }

            public class Money : Amount
            {
                public static bool operator {|SST2302:==|}(Money left, Money right) => true;

                public static bool operator !=(Money left, Money right) => false;
            }
            """);

    /// <summary>Verifies the <c>&lt;</c>/<c>&gt;</c> pair without the <c>&lt;=</c>/<c>&gt;=</c> pair is reported.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <remarks>The compiler pairs each operator with its mirror; it never asks for the other pair.</remarks>
    [Test]
    public async Task RelationalPairWithoutTheOrEqualPairIsReportedAsync()
        => await VerifyOperators.VerifyAnalyzerAsync(
            """
            using System;

            public class Level : IComparable<Level>
            {
                public int CompareTo(Level other) => 0;

                public static bool operator {|SST2302:<|}(Level left, Level right) => true;

                public static bool operator >(Level left, Level right) => false;
            }
            """);

    /// <summary>Verifies the <c>&lt;=</c>/<c>&gt;=</c> pair reports the missing <c>&lt;</c>/<c>&gt;</c> pair from its own site.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task OrEqualPairWithoutTheStrictPairIsReportedAsync()
        => await VerifyOperators.VerifyAnalyzerAsync(
            """
            using System;

            public class Level : IComparable<Level>
            {
                public int CompareTo(Level other) => 0;

                public static bool operator {|SST2302:<=|}(Level left, Level right) => true;

                public static bool operator >=(Level left, Level right) => false;
            }
            """);

    /// <summary>Verifies relational operators on a type that cannot be ordered any other way are reported.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task RelationalOperatorsWithoutComparableAreReportedAsync()
        => await VerifyOperators.VerifyAnalyzerAsync(
            """
            public class Level
            {
                public static bool operator {|SST2302:<|}(Level left, Level right) => true;

                public static bool operator >(Level left, Level right) => false;

                public static bool operator <=(Level left, Level right) => true;

                public static bool operator >=(Level left, Level right) => false;
            }
            """);

    /// <summary>Verifies a type missing both the other pair and the ordering contract is told so once.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task BothOrderingGapsAreReportedTogetherAsync()
        => await VerifyOperators.VerifyAnalyzerAsync(
            """
            public class Level
            {
                public static bool operator {|SST2302:<|}(Level left, Level right) => true;

                public static bool operator >(Level left, Level right) => false;
            }
            """);

    /// <summary>Verifies the complete ordering set is clean.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task CompleteOrderingSetIsCleanAsync()
        => await VerifyOperators.VerifyAnalyzerAsync(
            """
            using System;

            public struct Level : IComparable<Level>
            {
                public int CompareTo(Level other) => 0;

                public static bool operator <(Level left, Level right) => true;

                public static bool operator >(Level left, Level right) => false;

                public static bool operator <=(Level left, Level right) => true;

                public static bool operator >=(Level left, Level right) => false;
            }
            """);

    /// <summary>Verifies the non-generic ordering contract is accepted too.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task NonGenericComparableIsAcceptedAsync()
        => await VerifyOperators.VerifyAnalyzerAsync(
            """
            using System;

            public class Level : IComparable
            {
                public int CompareTo(object obj) => 0;

                public static bool operator <(Level left, Level right) => true;

                public static bool operator >(Level left, Level right) => false;

                public static bool operator <=(Level left, Level right) => true;

                public static bool operator >=(Level left, Level right) => false;
            }
            """);

    /// <summary>Verifies an operator the rule does not police is never examined.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task UnrelatedOperatorIsCleanAsync()
        => await VerifyOperators.VerifyAnalyzerAsync(
            """
            public class Flags
            {
                public static Flags operator &(Flags left, Flags right) => left;
            }
            """);

    /// <summary>Verifies a public class overloading arithmetic with no value equality is reported once, on the operator.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task ArithmeticOperatorWithoutValueEqualityIsReportedAsync()
        => await VerifyOperators.VerifyAnalyzerAsync(
            """
            public class Money
            {
                public static Money operator {|SST2302:+|}(Money left, Money right) => left;
            }
            """);

    /// <summary>Verifies a class overloading several arithmetic operators is reported once, from the first in the set.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task SeveralArithmeticOperatorsAreReportedOnceAsync()
        => await VerifyOperators.VerifyAnalyzerAsync(
            """
            public class Money
            {
                public static Money operator {|SST2302:+|}(Money left, Money right) => left;

                public static Money operator -(Money left, Money right) => left;

                public static Money operator *(Money left, int right) => left;
            }
            """);

    /// <summary>Verifies an arithmetic type that reports its multiply operator when it declares no addition.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task ArithmeticSetWithoutAdditionReportsFromMultiplyAsync()
        => await VerifyOperators.VerifyAnalyzerAsync(
            """
            public class Vector
            {
                public static Vector operator {|SST2302:*|}(Vector left, int right) => left;

                public static Vector operator /(Vector left, int right) => left;
            }
            """);

    /// <summary>Verifies a public class with arithmetic and value equality is clean.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task ArithmeticOperatorWithValueEqualityIsCleanAsync()
        => await VerifyOperators.VerifyAnalyzerAsync(
            """
            public class Money
            {
                public static Money operator +(Money left, Money right) => left;

                public override bool Equals(object obj) => true;

                public override int GetHashCode() => 0;
            }
            """);

    /// <summary>Verifies a struct overloading arithmetic without an equality override is not reported.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <remarks>A struct has value equality, so reference-equality surprise cannot occur.</remarks>
    [Test]
    public async Task ArithmeticStructWithoutEqualityIsCleanAsync()
        => await VerifyOperators.VerifyAnalyzerAsync(
            """
            public struct Money
            {
                public static Money operator +(Money left, Money right) => left;
            }
            """);

    /// <summary>Verifies a non-public class overloading arithmetic without equality is not reported.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task ArithmeticInternalClassWithoutEqualityIsCleanAsync()
        => await VerifyOperators.VerifyAnalyzerAsync(
            """
            internal class Money
            {
                public static Money operator +(Money left, Money right) => left;
            }
            """);

    /// <summary>Verifies a unary operator does not make a type an arithmetic type for this rule.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task UnaryOperatorWithoutEqualityIsCleanAsync()
        => await VerifyOperators.VerifyAnalyzerAsync(
            """
            public class Money
            {
                public static Money operator -(Money value) => value;
            }
            """);
}
