// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using Verify = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.Sst1662ThrownExceptionDocumentationAnalyzer,
    StyleSharp.Analyzers.Sst1662ThrownExceptionDocumentationCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST1662 (thrown exceptions should be documented).</summary>
public class ThrownExceptionDocumentationAnalyzerUnitTest
{
    /// <summary>Verifies a documented thrown exception produces no diagnostics.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task DocumentedThrowIsCleanAsync() =>
        Verify.VerifyAnalyzerAsync(
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task UndocumentedMemberIsIgnoredAsync() =>
        Verify.VerifyAnalyzerAsync(
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task ThrowInsideLambdaIsIgnoredAsync() =>
        Verify.VerifyAnalyzerAsync(
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

    /// <summary>Verifies a guarded throw is documented with the condition that reaches it.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    /// <remarks>
    /// The guard is written as code rather than turned into prose: it is what the reader has to satisfy,
    /// and restating it verbatim describes the trigger without the fix inventing a sentence.
    /// </remarks>
    [Test]
    public async Task GuardedThrowIsDocumentedWithItsConditionAsync()
    {
        const string Source = """
                              using System;

                              internal class C
                              {
                                  /// <summary>Does it.</summary>
                                  /// <param name="value">The value.</param>
                                  public void {|SST1662:M|}(string value)
                                  {
                                      if (value.Length == 0)
                                      {
                                          throw new InvalidOperationException();
                                      }
                                  }
                              }
                              """;
        const string FixedSource = """
                                   using System;

                                   internal class C
                                   {
                                       /// <summary>Does it.</summary>
                                       /// <param name="value">The value.</param>
                                       /// <exception cref="InvalidOperationException">Thrown when <c>value.Length == 0</c>.</exception>
                                       public void M(string value)
                                       {
                                           if (value.Length == 0)
                                           {
                                               throw new InvalidOperationException();
                                           }
                                       }
                                   }
                                   """;

        await Verify.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a null-coalescing throw is documented as the operand being null.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NullCoalescingThrowIsDocumentedAsANullOperandAsync()
    {
        const string Source = """
                              using System;

                              internal class C
                              {
                                  /// <summary>Does it.</summary>
                                  /// <param name="value">The value.</param>
                                  /// <returns>The value.</returns>
                                  public string {|SST1662:M|}(string value) =>
                                      value ?? throw new ArgumentNullException(nameof(value));
                              }
                              """;
        const string FixedSource = """
                                   using System;

                                   internal class C
                                   {
                                       /// <summary>Does it.</summary>
                                       /// <param name="value">The value.</param>
                                       /// <returns>The value.</returns>
                                       /// <exception cref="ArgumentNullException">Thrown when <c>value</c> is <see langword="null"/>.</exception>
                                       public string M(string value) =>
                                           value ?? throw new ArgumentNullException(nameof(value));
                                   }
                                   """;

        await Verify.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies an unconditional throw is reported but offers no fix.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    /// <remarks>
    /// There is no condition to state, so the only element the fix could write is an empty one — which is
    /// what SST1665 reports, and it calls that worse than the missing element this rule found. The sentence
    /// is the author's to write.
    /// </remarks>
    [Test]
    public async Task UnconditionalThrowOffersNoFixAsync()
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

        await Verify.VerifyCodeFixAsync(Source, Source);
    }
}
