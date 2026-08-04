// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyMethodGroupFix = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.Sst2239MethodGroupAnalyzer,
    StyleSharp.Analyzers.Sst2239MethodGroupCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for <see cref="Sst2239MethodGroupCodeFixProvider"/> (SST2239).</summary>
public class MethodGroupCodeFixProviderUnitTest
{
    /// <summary>Verifies a single-parameter forwarding lambda is replaced by the method group.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SimpleLambdaIsReplacedByTheMethodGroupAsync()
    {
        const string Source = """
                              using System;

                              public sealed class C
                              {
                                  public Func<int, int> M() => {|SST2239:x => Square(x)|};

                                  private static int Square(int value) => value * value;
                              }
                              """;
        const string FixedSource = """
                                   using System;

                                   public sealed class C
                                   {
                                       public Func<int, int> M() => Square;

                                       private static int Square(int value) => value * value;
                                   }
                                   """;
        await VerifyMethodGroupFix.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a multi-parameter forwarding lambda keeps the receiver of the call it forwards to.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ParenthesizedLambdaKeepsTheCallReceiverAsync()
    {
        const string Source = """
                              using System;

                              public sealed class Formatter
                              {
                                  public string Combine(string first, string second) => first + second;
                              }

                              public sealed class C
                              {
                                  public Func<string, string, string> M(Formatter formatter)
                                      => {|SST2239:(first, second) => formatter.Combine(first, second)|};
                              }
                              """;
        const string FixedSource = """
                                   using System;

                                   public sealed class Formatter
                                   {
                                       public string Combine(string first, string second) => first + second;
                                   }

                                   public sealed class C
                                   {
                                       public Func<string, string, string> M(Formatter formatter)
                                           => formatter.Combine;
                                   }
                                   """;
        await VerifyMethodGroupFix.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies Fix All replaces every forwarding lambda in a document in one pass.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task FixAllReplacesEveryForwardingLambdaAsync()
    {
        const string Source = """
                              using System;

                              public sealed class C
                              {
                                  public Func<int, int> First() => {|SST2239:x => Square(x)|};

                                  public Func<int, int> Second() => {|SST2239:y => Square(y)|};

                                  private static int Square(int value) => value * value;
                              }
                              """;
        const string FixedSource = """
                                   using System;

                                   public sealed class C
                                   {
                                       public Func<int, int> First() => Square;

                                       public Func<int, int> Second() => Square;

                                       private static int Square(int value) => value * value;
                                   }
                                   """;
        await VerifyMethodGroupFix.VerifyCodeFixAsync(Source, FixedSource);
    }
}
