// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyReferenceEquality = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.Sst1495ReferenceEqualityOnValueEqualTypeAnalyzer,
    StyleSharp.Analyzers.Sst1495ReferenceEqualityOnValueEqualTypeCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST1495 (reference equality on a type that defines value equality) and its fix.</summary>
public class ReferenceEqualityOnValueEqualTypeAnalyzerUnitTest
{
    /// <summary>A type that overrides Equals and leaves the operator comparing references.</summary>
    private const string ValueEqualType = """

        public class Money
        {
            public decimal Amount { get; set; }

            public override bool Equals(object obj) => obj is Money other && other.Amount == Amount;

            public override int GetHashCode() => Amount.GetHashCode();
        }
        """;

    /// <summary>Verifies '==' on a value-equal type is reported and rewritten as a null-safe Equals call.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task EqualsOperatorIsRewrittenAsync()
    {
        const string Source = $$"""
                              public sealed class C
                              {
                                  public bool Same(Money left, Money right) => {|SST1495:left == right|};
                              }
                              {{ValueEqualType}}
                              """;
        const string FixedSource = $$"""
                                   public sealed class C
                                   {
                                       public bool Same(Money left, Money right) => object.Equals(left, right);
                                   }
                                   {{ValueEqualType}}
                                   """;
        await VerifyReferenceEquality.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies '!=' is rewritten as a negated Equals call.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NotEqualsOperatorIsNegatedAsync()
    {
        const string Source = $$"""
                              public sealed class C
                              {
                                  public bool Differ(Money left, Money right) => {|SST1495:left != right|};
                              }
                              {{ValueEqualType}}
                              """;
        const string FixedSource = $$"""
                                   public sealed class C
                                   {
                                       public bool Differ(Money left, Money right) => !object.Equals(left, right);
                                   }
                                   {{ValueEqualType}}
                                   """;
        await VerifyReferenceEquality.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies the rewrite keeps the surrounding expression intact.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task RewriteInsideLargerExpressionKeepsPrecedenceAsync()
    {
        const string Source = $$"""
                              public sealed class C
                              {
                                  public bool Check(Money left, Money right, bool flag) => {|SST1495:left != right|} && flag;
                              }
                              {{ValueEqualType}}
                              """;
        const string FixedSource = $$"""
                                   public sealed class C
                                   {
                                       public bool Check(Money left, Money right, bool flag) => !object.Equals(left, right) && flag;
                                   }
                                   {{ValueEqualType}}
                                   """;
        await VerifyReferenceEquality.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies an inherited Equals override is enough to make the operator disagree.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task InheritedEqualsOverrideIsReportedAsync()
        => await VerifyReferenceEquality.VerifyAnalyzerAsync(
            $$"""
            public sealed class Cash : Money
            {
            }

            public sealed class C
            {
                public bool Same(Cash left, Cash right) => {|SST1495:left == right|};
            }
            {{ValueEqualType}}
            """);

    /// <summary>Verifies a comparison against null is a null check and is never reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NullComparisonIsCleanAsync()
        => await VerifyReferenceEquality.VerifyAnalyzerAsync(
            $$"""
            public sealed class C
            {
                public bool Missing(Money value) => value == null || null != value;
            }
            {{ValueEqualType}}
            """);

    /// <summary>Verifies a type that overloads the operator means what it says.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task TypeThatOverloadsTheOperatorIsCleanAsync()
        => await VerifyReferenceEquality.VerifyAnalyzerAsync(
            """
            public class Weight
            {
                public int Grams { get; set; }

                public static bool operator ==(Weight left, Weight right) => Equals(left, right);

                public static bool operator !=(Weight left, Weight right) => !Equals(left, right);

                public override bool Equals(object obj) => obj is Weight other && other.Grams == Grams;

                public override int GetHashCode() => Grams;
            }

            public sealed class C
            {
                public bool Same(Weight left, Weight right) => left == right;
            }
            """);

    /// <summary>Verifies a type that never overrides Equals compares references on purpose.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task TypeWithoutEqualsOverrideIsCleanAsync()
        => await VerifyReferenceEquality.VerifyAnalyzerAsync(
            """
            public class Session
            {
                public int Id { get; set; }
            }

            public sealed class C
            {
                public bool Same(Session left, Session right) => left == right;
            }
            """);

    /// <summary>Verifies the framework types that already overload the operator are left alone.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task StringsRecordsAndValueTypesAreCleanAsync()
        => await VerifyReferenceEquality.VerifyAnalyzerAsync(
            """
            public record Point(int X, int Y);

            public sealed class C
            {
                public bool Text(string left, string right) => left == right;

                public bool Records(Point left, Point right) => left == right;

                public bool Numbers(int left, int right) => left == right;

                public bool Delegates(System.Action left, System.Action right) => left == right;
            }

            namespace System.Runtime.CompilerServices
            {
                internal static class IsExternalInit
                {
                }
            }
            """);

    /// <summary>Verifies an operand typed as object is an explicit request for reference semantics.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ObjectTypedOperandIsCleanAsync()
        => await VerifyReferenceEquality.VerifyAnalyzerAsync(
            $$"""
            public sealed class C
            {
                public bool Same(Money left, object right) => left == right;
            }
            {{ValueEqualType}}
            """);

    /// <summary>Verifies an interface operand and an unconstrained type parameter are both left alone.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task InterfaceAndTypeParameterOperandsAreCleanAsync()
        => await VerifyReferenceEquality.VerifyAnalyzerAsync(
            """
            public interface IAmount
            {
                decimal Amount { get; }
            }

            public sealed class C
            {
                public bool Interfaces(IAmount left, IAmount right) => left == right;

                public bool Generic<T>(T left, T right)
                    where T : class => left == right;
            }
            """);
}
