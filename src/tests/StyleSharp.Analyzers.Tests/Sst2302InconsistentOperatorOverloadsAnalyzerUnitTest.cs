// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using RoslynCommon.Analyzers.Tests;
using VerifyOperators = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<StyleSharp.Analyzers.Sst2302InconsistentOperatorOverloadsAnalyzer>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST2302 (overload operators in their complete set).</summary>
public class Sst2302InconsistentOperatorOverloadsAnalyzerUnitTest
{
    /// <summary>The arithmetic type name used in diagnostic arguments.</summary>
    private const string MoneyTypeName = "Money";

    /// <summary>Verifies the missing equality override is named when a hash override already exists.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task EqualityOperatorWithoutEqualsIsReportedAsync() => VerifyOperators.VerifyAnalyzerAsync(
        """
        public class Money
        {
            public static bool operator {|#0:==|}(Money left, Money right) => true;
            public static bool operator !=(Money left, Money right) => false;
            public override int GetHashCode() => 0;
        }
        """,
        VerifyOperators.Diagnostic().WithLocation(0).WithArguments(MoneyTypeName, "==", "Equals(object)"));

    /// <summary>Verifies members with equality names must be overrides to satisfy equality operators.</summary>
    /// <param name="members">The unrelated members sharing equality method names.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("public new bool Equals(object other) => true; public new int GetHashCode() => 0;")]
    [Arguments("public new int Equals; public new int GetHashCode;")]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task EqualityNamesWithoutOverridesAreReportedAsync(string members) => VerifyOperators.VerifyAnalyzerAsync(
        $$"""
        public class Money
        {
            public static bool operator {|SST2302:==|}(Money left, Money right) => true;
            public static bool operator !=(Money left, Money right) => false;
            {{members}}
        }
        """);

    /// <summary>Verifies overrides with different parameter counts do not satisfy equality operators.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task EqualityOverridesWithDifferentParameterCountsAreReportedAsync() => VerifyOperators.VerifyAnalyzerAsync(
        """
        public class Amount
        {
            public virtual bool Equals(object first, object second) => true;
            public virtual int GetHashCode(int seed) => seed;
        }
        public class Money : Amount
        {
            public static bool operator {|SST2302:==|}(Money left, Money right) => true;
            public static bool operator !=(Money left, Money right) => false;
            public override bool Equals(object first, object second) => true;
            public override int GetHashCode(int seed) => seed;
        }
        """);

    /// <summary>Verifies a matching equality override remains discoverable after a different overload.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task EqualityOverloadsBeforeObjectOverridesAreCleanAsync() => VerifyOperators.VerifyAnalyzerAsync(
        """
        public class Money
        {
            public static bool operator ==(Money left, Money right) => true;
            public static bool operator !=(Money left, Money right) => false;
            public bool Equals(Money other) => true;
            public override bool Equals(object other) => true;
            public int GetHashCode(int seed) => seed;
            public override int GetHashCode() => 0;
        }
        """);

    /// <summary>Verifies arithmetic reporting follows operator precedence even when declarations are reversed.</summary>
    /// <param name="primary">The first operator in the analyzer's precedence order.</param>
    /// <param name="later">An operator later in that order.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("-", "*")]
    [Arguments("-", "/")]
    [Arguments("-", "%")]
    [Arguments("*", "%")]
    [Arguments("/", "%")]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task EarliestArithmeticOperatorOwnsTheDiagnosticAsync(string primary, string later) => VerifyOperators.VerifyAnalyzerAsync(
        $$"""
        public class Money
        {
            public static Money operator {{later}}(Money left, Money right) => left;
            public static Money operator {|#0:{{primary}}|}(Money left, Money right) => left;
        }
        """,
        VerifyOperators.Diagnostic().WithLocation(0).WithArguments(MoneyTypeName, primary, "==, Equals(object) or GetHashCode()"));

    /// <summary>Verifies division and remainder each report when they are the only arithmetic operator.</summary>
    /// <param name="operatorText">The binary operator to declare.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("/")]
    [Arguments("%")]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task LoneTrailingArithmeticOperatorIsReportedAsync(string operatorText) => VerifyOperators.VerifyAnalyzerAsync(
        $$"""
        public class Money
        {
            public static Money operator {|#0:{{operatorText}}|}(Money left, Money right) => left;
        }
        """,
        VerifyOperators.Diagnostic().WithLocation(0).WithArguments(MoneyTypeName, operatorText, "==, Equals(object) or GetHashCode()"));

    /// <summary>Verifies any declared equality override is enough to avoid the arithmetic diagnostic.</summary>
    /// <param name="member">The value equality member declared by the arithmetic class.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("public override bool Equals(object other) => true;")]
    [Arguments("public override int GetHashCode() => 0;")]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task ArithmeticWithOneEqualityOverrideIsCleanAsync(string member) => VerifyOperators.VerifyAnalyzerAsync(
        $$"""
        public class Money
        {
            public static Money operator +(Money left, Money right) => left;
            {{member}}
        }
        """);

    /// <summary>Verifies an equality operator owns missing overrides even when the type also declares arithmetic.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task ArithmeticWithEqualityOperatorOnlyReportsEqualityGapAsync() => VerifyOperators.VerifyAnalyzerAsync(
        """
        public class Money
        {
            public static Money operator +(Money left, Money right) => left;
            public static bool operator {|SST2302:==|}(Money left, Money right) => true;
            public static bool operator !=(Money left, Money right) => false;
        }
        """);

    /// <summary>Verifies generated record equality satisfies an arithmetic class.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task ArithmeticRecordHasValueEqualityAsync() => VerifyOperators.VerifyAnalyzerAsync(
        "public record Money { public static Money operator +(Money left, Money right) => left; }");

    /// <summary>Verifies unrelated interfaces and lookalike ordering contracts do not satisfy the framework contract.</summary>
    /// <param name="declaration">The interface declaration to implement.</param>
    /// <param name="interfaceName">The fully qualified interface name.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("public interface IOther { }", "IOther")]
    [Arguments("public interface IComparable { }", "IComparable")]
    [Arguments("namespace Other { public interface IComparable { } }", "Other.IComparable")]
    [Arguments("namespace Other.System { public interface IComparable { } }", "Other.System.IComparable")]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task LookalikeComparableDoesNotSatisfyOrderingAsync(string declaration, string interfaceName) => VerifyOperators.VerifyAnalyzerAsync(
        $$"""
        {{declaration}}
        public class Level : {{interfaceName}}
        {
            public static bool operator {|SST2302:<|}(Level left, Level right) => true;
            public static bool operator >(Level left, Level right) => false;
            public static bool operator <=(Level left, Level right) => true;
            public static bool operator >=(Level left, Level right) => false;
        }
        """);

    /// <summary>Verifies an unrelated interface does not prevent finding an inherited generic ordering contract.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task InheritedComparableAfterUnrelatedInterfaceIsCleanAsync() => VerifyOperators.VerifyAnalyzerAsync(
        """
        public interface IOther { }
        public class Ordered : System.IComparable<Level>
        {
            public int CompareTo(Level other) => 0;
        }
        public class Level : Ordered, IOther
        {
            public static bool operator <(Level left, Level right) => true;
            public static bool operator >(Level left, Level right) => false;
            public static bool operator <=(Level left, Level right) => true;
            public static bool operator >=(Level left, Level right) => false;
        }
        """);

    /// <summary>Verifies the non-strict relational pair names both missing ordering requirements.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task OrEqualPairWithoutComparableNamesBothGapsAsync() => VerifyOperators.VerifyAnalyzerAsync(
        """
        public class Level
        {
            public static bool operator {|#0:<=|}(Level left, Level right) => true;
            public static bool operator >=(Level left, Level right) => false;
        }
        """,
        VerifyOperators.Diagnostic().WithLocation(0).WithArguments("Level", "<=", "< and > and IComparable<Level>"));

    /// <summary>Verifies an unavailable framework ordering contract is never suggested.</summary>
    /// <param name="otherPair">The remaining pair, or empty text to leave an ordering gap.</param>
    /// <param name="expectedCount">The number of diagnostics caused by an incomplete operator set.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("", 1)]
    [Arguments("public static bool operator <=(Level left, Level right) => true; public static bool operator >=(Level left, Level right) => false;", 0)]
    public async Task MissingFrameworkComparableIsNotSuggestedAsync(string otherPair, int expectedCount)
    {
        var compilation = CSharpCompilation.Create(
            nameof(MissingFrameworkComparableIsNotSuggestedAsync),
            [CSharpSyntaxTree.ParseText(
                $$"""
                namespace System
                {
                    public class Object { }
                    public struct Boolean { }
                }
                public class Level
                {
                    public static bool operator <(Level left, Level right) => true;
                    public static bool operator >(Level left, Level right) => false;
                    {{otherPair}}
                }
                """)]);
        await Assert.That(compilation.GetTypeByMetadataName("System.IComparable`1")).IsNull();
        var diagnostics = await compilation.WithAnalyzers([new Sst2302InconsistentOperatorOverloadsAnalyzer()]).GetAnalyzerDiagnosticsAsync();
        await Assert.That(diagnostics.Length).IsEqualTo(expectedCount);
        foreach (var diagnostic in diagnostics)
        {
            await Assert.That(diagnostic.Id).IsEqualTo("SST2302");
            await Assert.That(diagnostic.GetMessage()).DoesNotContain("IComparable");
        }
    }

    /// <summary>Verifies a malformed namespace-level operator is analyzed through its recovery type.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task NamespaceLevelOperatorUsesItsRecoveryTypeAsync()
    {
        var tree = CSharpSyntaxTree.ParseText("namespace N { public static bool operator ==(int left, int right) => true; }");
        var compilation = CSharpCompilation.Create(
            nameof(NamespaceLevelOperatorUsesItsRecoveryTypeAsync),
            [tree],
            RuntimeMetadataReferences.Platform);
        var root = await tree.GetRootAsync();
        var declaration = root.DescendantNodes().OfType<OperatorDeclarationSyntax>().Single();
        await Assert.That(compilation.GetSemanticModel(tree).GetDeclaredSymbol(declaration)).IsNotNull();
        var diagnostics = await compilation.WithAnalyzers([new Sst2302InconsistentOperatorOverloadsAnalyzer()]).GetAnalyzerDiagnosticsAsync();
        await Assert.That(diagnostics.Length).IsEqualTo(1);
        await Assert.That(diagnostics[0].Id).IsEqualTo("SST2302");
        await Assert.That(diagnostics[0].Location.SourceSpan).IsEqualTo(declaration.OperatorToken.Span);
    }

    /// <summary>Verifies <c>==</c> without either equality override is reported once, on the operator.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task EqualityOperatorWithoutEqualityOverridesIsReportedAsync() =>
        VerifyOperators.VerifyAnalyzerAsync(
            """
            public class Money
            {
                public static bool operator {|SST2302:==|}(Money left, Money right) => true;

                public static bool operator !=(Money left, Money right) => false;
            }
            """);

    /// <summary>Verifies <c>==</c> with an <c>Equals(object)</c> override but no hash is still reported.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task EqualityOperatorWithoutHashCodeIsReportedAsync() =>
        VerifyOperators.VerifyAnalyzerAsync(
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task CompleteEqualitySetIsCleanAsync() =>
        VerifyOperators.VerifyAnalyzerAsync(
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task InheritedEqualityOverrideDoesNotCountAsync() =>
        VerifyOperators.VerifyAnalyzerAsync(
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task RelationalPairWithoutTheOrEqualPairIsReportedAsync() =>
        VerifyOperators.VerifyAnalyzerAsync(
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task OrEqualPairWithoutTheStrictPairIsReportedAsync() =>
        VerifyOperators.VerifyAnalyzerAsync(
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task RelationalOperatorsWithoutComparableAreReportedAsync() =>
        VerifyOperators.VerifyAnalyzerAsync(
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task BothOrderingGapsAreReportedTogetherAsync() =>
        VerifyOperators.VerifyAnalyzerAsync(
            """
            public class Level
            {
                public static bool operator {|SST2302:<|}(Level left, Level right) => true;

                public static bool operator >(Level left, Level right) => false;
            }
            """);

    /// <summary>Verifies the complete ordering set is clean.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task CompleteOrderingSetIsCleanAsync() =>
        VerifyOperators.VerifyAnalyzerAsync(
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task NonGenericComparableIsAcceptedAsync() =>
        VerifyOperators.VerifyAnalyzerAsync(
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task UnrelatedOperatorIsCleanAsync() =>
        VerifyOperators.VerifyAnalyzerAsync(
            """
            public class Flags
            {
                public static Flags operator &(Flags left, Flags right) => left;
            }
            """);

    /// <summary>Verifies a public class overloading arithmetic with no value equality is reported once, on the operator.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task ArithmeticOperatorWithoutValueEqualityIsReportedAsync() =>
        VerifyOperators.VerifyAnalyzerAsync(
            """
            public class Money
            {
                public static Money operator {|SST2302:+|}(Money left, Money right) => left;
            }
            """);

    /// <summary>Verifies a class overloading several arithmetic operators is reported once, from the first in the set.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task SeveralArithmeticOperatorsAreReportedOnceAsync() =>
        VerifyOperators.VerifyAnalyzerAsync(
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task ArithmeticSetWithoutAdditionReportsFromMultiplyAsync() =>
        VerifyOperators.VerifyAnalyzerAsync(
            """
            public class Vector
            {
                public static Vector operator {|SST2302:*|}(Vector left, int right) => left;

                public static Vector operator /(Vector left, int right) => left;
            }
            """);

    /// <summary>Verifies a public class with arithmetic and value equality is clean.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task ArithmeticOperatorWithValueEqualityIsCleanAsync() =>
        VerifyOperators.VerifyAnalyzerAsync(
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task ArithmeticStructWithoutEqualityIsCleanAsync() =>
        VerifyOperators.VerifyAnalyzerAsync(
            """
            public struct Money
            {
                public static Money operator +(Money left, Money right) => left;
            }
            """);

    /// <summary>Verifies a non-public class overloading arithmetic without equality is not reported.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task ArithmeticInternalClassWithoutEqualityIsCleanAsync() =>
        VerifyOperators.VerifyAnalyzerAsync(
            """
            internal class Money
            {
                public static Money operator +(Money left, Money right) => left;
            }
            """);

    /// <summary>Verifies a unary operator does not make a type an arithmetic type for this rule.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task UnaryOperatorWithoutEqualityIsCleanAsync() =>
        VerifyOperators.VerifyAnalyzerAsync(
            """
            public class Money
            {
                public static Money operator -(Money value) => value;
            }
            """);
}
