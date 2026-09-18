// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using Verify = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    StyleSharp.Analyzers.Sst1452UnusedTypeParameterAnalyzer>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Tests for <see cref="Sst1452UnusedTypeParameterAnalyzer"/> (SST1452 unused type parameters).</summary>
public class Sst1452UnusedTypeParameterAnalyzerUnitTest
{
    /// <summary>Verifies a self-referential constraint uses its type parameter with either abstraction policy.</summary>
    /// <param name="modifier">The base class abstraction modifier.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("")]
    [Arguments("abstract ")]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task SelfReferentialConstraintUsesTypeParameterAsync(string modifier) =>
        Verify.VerifyAnalyzerAsync(
            $$"""
            public {{modifier}}class Runner<T> where T : Runner<T>, new()
            {
                public int Value => 1;
            }

            public class SchemaTests : Runner<SchemaTests> { }
            """);

    /// <summary>Verifies local functions count signature and body references independently.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task LocalFunctionTypeParametersAreCheckedAsync() =>
        Verify.VerifyAnalyzerAsync(
            """
            class C
            {
                void M()
                {
                    void Unused<{|SST1452:T|}>() { }
                    T Identity<T>(T value) => value;
                    object Body<T>() => typeof(T);
                    void Nongeneric() { }
                }
            }
            """);

    /// <summary>Verifies every registered type declaration checks unused parameters.</summary>
    /// <param name="declaration">The generic declaration.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("struct C<{|SST1452:T|}> { }")]
    [Arguments("interface C<{|SST1452:T|}> { }")]
    [Arguments("record C<{|SST1452:T|}>;")]
    [Arguments("record struct C<{|SST1452:T|}>;")]
    [Arguments("record C<T>(T Value);")]
    [Arguments("record struct C<T>(T Value);")]
    [Arguments("interface C<T> { T Value { get; } }")]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task TypeDeclarationShapesAreCheckedAsync(string declaration) =>
        Verify.VerifyAnalyzerAsync(declaration);

    /// <summary>Verifies the final bit is tracked at the arity limit and larger declarations are skipped.</summary>
    /// <param name="count">The declaration's number of type parameters.</param>
    /// <param name="reports">Whether the unused final parameter is tracked.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments(64, true)]
    [Arguments(65, false)]
    public async Task ArityLimitControlsTrackingAsync(int count, bool reports)
    {
        var parameters = string.Join(", ", Enumerable.Range(0, count - 1).Select(static index => $"T{index}"));
        var arguments = string.Join(", ", Enumerable.Range(0, count - 1).Select(static index => $"T{index} value{index}"));
        await Verify.VerifyAnalyzerAsync($$"""class C { void M<{{parameters}}, {{(reports ? "{|SST1452:TLast|}" : "TLast")}}>({{arguments}}) { } }""");
    }

    /// <summary>Verifies virtual, partial and explicit interface methods retain their declared arity.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task LockedMethodArityIsIgnoredAsync() =>
        Verify.VerifyAnalyzerAsync(
            """
            interface I { public abstract void M<T>(); }
            partial class C : I
            {
                public virtual void Virtual<T>() { }
                partial void Partial<T>();
                partial void Partial<T>() { }
                void I.M<T>() { }
            }
            """);

    /// <summary>Verifies an unused method type parameter is flagged.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task UnusedMethodTypeParameterIsFlaggedAsync() =>
        Verify.VerifyAnalyzerAsync(
            """
            public class C
            {
                public void M<{|SST1452:T|}>(int value)
                {
                }
            }
            """);

    /// <summary>Verifies a type parameter used in the signature is clean.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task SignatureUsageIsCleanAsync() =>
        Verify.VerifyAnalyzerAsync(
            """
            public class C
            {
                public T M<T>(T value) => value;
            }
            """);

    /// <summary>Verifies a type parameter used only in the body is clean.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task BodyUsageIsCleanAsync() =>
        Verify.VerifyAnalyzerAsync(
            """
            public class C
            {
                public object M<T>() => typeof(T);
            }
            """);

    /// <summary>Verifies a parameter appearing only as its own constraint clause name is flagged.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task ConstraintOnlyParameterIsFlaggedAsync() =>
        Verify.VerifyAnalyzerAsync(
            """
            public class C
            {
                public void M<{|SST1452:T|}>() where T : class
                {
                }
            }
            """);

    /// <summary>Verifies usage inside another parameter's constraint counts.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task UsageInOtherConstraintIsCleanAsync() =>
        Verify.VerifyAnalyzerAsync(
            """
            using System.Collections.Generic;

            public class C
            {
                public void M<T, U>(U value) where U : IList<T>
                {
                }
            }
            """);

    /// <summary>Verifies an unused class type parameter is flagged.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task UnusedClassTypeParameterIsFlaggedAsync() =>
        Verify.VerifyAnalyzerAsync(
            """
            public class Holder<{|SST1452:T|}>
            {
                public int Count { get; set; }
            }
            """);

    /// <summary>Verifies polymorphic and partial declarations are skipped.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task PolymorphicAndPartialDeclarationsAreSkippedAsync() =>
        Verify.VerifyAnalyzerAsync(
            """
            public abstract class B
            {
                public abstract void M<T>();
            }

            public class C : B
            {
                public override void M<T>()
                {
                }
            }

            public partial class P<T>
            {
            }
            """);
}
