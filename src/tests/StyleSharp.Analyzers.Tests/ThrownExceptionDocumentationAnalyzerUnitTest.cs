// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Verify = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.Sst1662ThrownExceptionDocumentationAnalyzer,
    StyleSharp.Analyzers.Sst1662ThrownExceptionDocumentationCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST1662 (thrown exceptions should be documented).</summary>
public class ThrownExceptionDocumentationAnalyzerUnitTest
{
    /// <summary>Verifies a documented thrown exception produces no diagnostics.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task DocumentedThrowIsCleanAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            using System;

            internal class C
            {
                /// <summary>Does it.</summary>
                /// <exception cref="InvalidOperationException">When bad.</exception>
                public void M()
                {
                    throw new InvalidOperationException();
                }
            }
            """);

    /// <summary>Verifies an undocumented member (no documentation comment) is left to the coverage rules.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task UndocumentedMemberIsIgnoredAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            using System;

            internal class C
            {
                public void M()
                {
                    throw new InvalidOperationException();
                }
            }
            """);

    /// <summary>Verifies a throw inside a lambda is not attributed to the enclosing member.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ThrowInsideLambdaIsIgnoredAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            using System;

            internal class C
            {
                /// <summary>Does it.</summary>
                public void M()
                {
                    Action a = () => throw new InvalidOperationException();
                    a();
                }
            }
            """);

    /// <summary>Verifies an undocumented thrown exception is reported and an <c>&lt;exception&gt;</c> skeleton is added.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task UndocumentedThrowGetsExceptionElementAsync()
    {
        const string Source = """
                              using System;

                              internal class C
                              {
                                  /// <summary>Does it.</summary>
                                  public void {|SST1662:M|}()
                                  {
                                      throw new InvalidOperationException();
                                  }
                              }
                              """;
        const string FixedSource = """
                                   using System;

                                   internal class C
                                   {
                                       /// <summary>Does it.</summary>
                                       /// <exception cref="InvalidOperationException"></exception>
                                       public void M()
                                       {
                                           throw new InvalidOperationException();
                                       }
                                   }
                                   """;

        await Verify.VerifyCodeFixAsync(Source, FixedSource);
    }
}
