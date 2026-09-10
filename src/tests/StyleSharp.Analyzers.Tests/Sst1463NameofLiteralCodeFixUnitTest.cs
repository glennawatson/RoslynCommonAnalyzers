// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyNameofLiteralFix = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.Sst1463NameofLiteralAnalyzer,
    StyleSharp.Analyzers.Sst1463NameofLiteralCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for the SST1463 code fix (use nameof for symbol-name strings).</summary>
public class Sst1463NameofLiteralCodeFixUnitTest
{
    /// <summary>Verifies a property-name literal becomes a nameof expression.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task PropertyNameLiteralBecomesNameofAsync()
    {
        const string Source = """
                              public sealed class C
                              {
                                  public int Count { get; set; }

                                  public void M() => Notify({|SST1463:"Count"|});

                                  private static void Notify(string propertyName)
                                  {
                                  }
                              }
                              """;
        const string FixedSource = """
                                   public sealed class C
                                   {
                                       public int Count { get; set; }

                                       public void M() => Notify(nameof(Count));

                                       private static void Notify(string propertyName)
                                       {
                                       }
                                   }
                                   """;
        await VerifyNameofLiteralFix.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a named argument keeps its name when the literal is replaced.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task NamedArgumentKeepsItsNameAsync()
    {
        const string Source = """
                              public sealed class C
                              {
                                  public int Count { get; set; }

                                  public void M() => Notify(propertyName: {|SST1463:"Count"|});

                                  private static void Notify(string propertyName)
                                  {
                                  }
                              }
                              """;
        const string FixedSource = """
                                   public sealed class C
                                   {
                                       public int Count { get; set; }

                                       public void M() => Notify(propertyName: nameof(Count));

                                       private static void Notify(string propertyName)
                                       {
                                       }
                                   }
                                   """;
        await VerifyNameofLiteralFix.VerifyCodeFixAsync(Source, FixedSource);
    }
}
