// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyParameterName = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.MethodNamingAnalyzer,
    StyleSharp.Analyzers.NamingRenameCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST1318 (overriding parameter names) and its rename fix.</summary>
public class ParameterNameMatchesBaseAnalyzerUnitTest
{
    /// <summary>Verifies an override whose parameter name differs from the base is reported and renamed.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task OverrideParameterRenamedToBaseAsync()
    {
        const string Source = """
                              public abstract class B
                              {
                                  public abstract void Process(int count);
                              }

                              public class C : B
                              {
                                  public override void Process(int {|SST1318:value|})
                                  {
                                      System.Console.WriteLine(value);
                                  }
                              }
                              """;
        const string FixedSource = """
                                   public abstract class B
                                   {
                                       public abstract void Process(int count);
                                   }

                                   public class C : B
                                   {
                                       public override void Process(int count)
                                       {
                                           System.Console.WriteLine(count);
                                       }
                                   }
                                   """;
        await VerifyParameterName.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies an interface implementation with a mismatched parameter is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task InterfaceImplementationMismatchReportedAsync()
        => await VerifyParameterName.VerifyAnalyzerAsync(
            """
            public interface IProcessor
            {
                void Run(int iterations);
            }

            public class C : IProcessor
            {
                public void Run(int {|SST1318:n|})
                {
                    System.Console.WriteLine(n);
                }
            }
            """);

    /// <summary>Verifies a matching override and a non-overriding method are not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task MatchingNamesAreCleanAsync()
        => await VerifyParameterName.VerifyAnalyzerAsync(
            """
            public abstract class B
            {
                public abstract void Process(int count);
            }

            public class C : B
            {
                public override void Process(int count)
                {
                    System.Console.WriteLine(count);
                }

                public void Helper(int value)
                {
                    System.Console.WriteLine(value);
                }
            }
            """);
}
