// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyInheritance = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.RedundantCodeAnalyzer,
    StyleSharp.Analyzers.RedundantInheritanceListCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST1177 (redundant base types) and its fix.</summary>
public class RedundantInheritanceListAnalyzerUnitTest
{
    /// <summary>Verifies an explicit <c>object</c> base is reported and removed.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ExplicitObjectBaseRemovedAsync()
    {
        const string Source = """
                              public class C : {|SST1177:object|}
                              {
                              }
                              """;
        const string FixedSource = """
                                   public class C
                                   {
                                   }
                                   """;
        await VerifyInheritance.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies an explicit <c>int</c> enum underlying type is reported and removed.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ExplicitIntEnumBaseRemovedAsync()
    {
        const string Source = """
                              public enum E : {|SST1177:int|}
                              {
                                  A,
                              }
                              """;
        const string FixedSource = """
                                   public enum E
                                   {
                                       A,
                                   }
                                   """;
        await VerifyInheritance.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies Fix All removes every redundant inheritance entry across a document in one pass.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task FixAllRewritesEveryOccurrenceAsync()
    {
        const string Source = """
                              public class A : {|SST1177:object|}
                              {
                              }

                              public class B : {|SST1177:object|}
                              {
                              }

                              public enum E : {|SST1177:int|}
                              {
                                  X,
                              }
                              """;
        const string FixedSource = """
                                   public class A
                                   {
                                   }

                                   public class B
                                   {
                                   }

                                   public enum E
                                   {
                                       X,
                                   }
                                   """;
        await VerifyInheritance.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a real base type and a non-int enum underlying type are not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task RealBasesAreCleanAsync()
        => await VerifyInheritance.VerifyAnalyzerAsync(
            """
            public class Base
            {
            }

            public class C : Base
            {
            }

            public enum E : byte
            {
                A,
            }
            """);
}
