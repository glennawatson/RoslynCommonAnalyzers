// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Verify = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    StyleSharp.Analyzers.Sst1452UnusedTypeParameterAnalyzer>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Tests for <see cref="Sst1452UnusedTypeParameterAnalyzer"/> (SST1452 unused type parameters).</summary>
public class Sst1452UnusedTypeParameterAnalyzerUnitTest
{
    /// <summary>Verifies an unused method type parameter is flagged.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task UnusedMethodTypeParameterIsFlaggedAsync()
        => await Verify.VerifyAnalyzerAsync(
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
    [Test]
    public async Task SignatureUsageIsCleanAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            public class C
            {
                public T M<T>(T value) => value;
            }
            """);

    /// <summary>Verifies a type parameter used only in the body is clean.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task BodyUsageIsCleanAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            public class C
            {
                public object M<T>() => typeof(T);
            }
            """);

    /// <summary>Verifies a parameter appearing only as its own constraint clause name is flagged.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task ConstraintOnlyParameterIsFlaggedAsync()
        => await Verify.VerifyAnalyzerAsync(
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
    [Test]
    public async Task UsageInOtherConstraintIsCleanAsync()
        => await Verify.VerifyAnalyzerAsync(
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
    [Test]
    public async Task UnusedClassTypeParameterIsFlaggedAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            public class Holder<{|SST1452:T|}>
            {
                public int Count { get; set; }
            }
            """);

    /// <summary>Verifies polymorphic and partial declarations are skipped.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task PolymorphicAndPartialDeclarationsAreSkippedAsync()
        => await Verify.VerifyAnalyzerAsync(
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
