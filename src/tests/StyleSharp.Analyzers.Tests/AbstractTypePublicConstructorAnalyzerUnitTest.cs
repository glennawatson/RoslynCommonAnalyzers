// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyAbstractCtor = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.TypeDesignAnalyzer,
    StyleSharp.Analyzers.AbstractTypePublicConstructorCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST1428 (public constructor on an abstract type) and its fix.</summary>
public class AbstractTypePublicConstructorAnalyzerUnitTest
{
    /// <summary>Verifies a public constructor on an abstract class is reported and made protected.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task PublicConstructorMadeProtectedAsync()
    {
        const string Source = """
                              public abstract class C
                              {
                                  {|SST1428:public|} C()
                                  {
                                  }
                              }
                              """;
        const string FixedSource = """
                                   public abstract class C
                                   {
                                       protected C()
                                       {
                                       }
                                   }
                                   """;
        await VerifyAbstractCtor.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a protected abstract constructor and a public concrete constructor are not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ProtectedAndConcreteAreCleanAsync()
        => await VerifyAbstractCtor.VerifyAnalyzerAsync(
            """
            public abstract class A
            {
                protected A()
                {
                }
            }

            public class B
            {
                public B()
                {
                }
            }
            """);
}
