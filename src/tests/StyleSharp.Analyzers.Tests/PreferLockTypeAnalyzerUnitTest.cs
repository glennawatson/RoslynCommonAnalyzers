// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Testing;

using VerifyLock = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.PreferLockTypeAnalyzer,
    StyleSharp.Analyzers.PreferLockTypeCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST1900 (use System.Threading.Lock for a dedicated lock object) and its code fix.</summary>
public class PreferLockTypeAnalyzerUnitTest
{
    /// <summary>Verifies a lock-only object field is reported (SST1900) and its type changed to Lock.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task LockOnlyObjectFieldReplacedAsync()
    {
        const string Source = """
                              public class C
                              {
                                  private readonly object {|SST1900:_gate|} = new();

                                  public void M()
                                  {
                                      lock (_gate)
                                      {
                                      }
                                  }
                              }
                              """;
        const string FixedSource = """
                                   public class C
                                   {
                                       private readonly System.Threading.Lock _gate = new();

                                       public void M()
                                       {
                                           lock (_gate)
                                           {
                                           }
                                       }
                                   }
                                   """;
        await VerifyNet90Async(Source, FixedSource);
    }

    /// <summary>Verifies a <c>new object()</c> initializer is normalised to <c>new()</c> by the fix.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ExplicitObjectInitializerNormalisedAsync()
    {
        const string Source = """
                              public class C
                              {
                                  private readonly object {|SST1900:_sync|} = new object();

                                  public void M()
                                  {
                                      lock (_sync)
                                      {
                                      }
                                  }
                              }
                              """;
        const string FixedSource = """
                                   public class C
                                   {
                                       private readonly System.Threading.Lock _sync = new();

                                       public void M()
                                       {
                                           lock (_sync)
                                           {
                                           }
                                       }
                                   }
                                   """;
        await VerifyNet90Async(Source, FixedSource);
    }

    /// <summary>Verifies an explicitly qualified System.Object field is also reported and fixed.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task QualifiedObjectTypeReplacedAsync()
    {
        const string Source = """
                              public class C
                              {
                                  private readonly System.Object {|SST1900:_gate|} = new();

                                  public void M()
                                  {
                                      lock (_gate)
                                      {
                                      }
                                  }
                              }
                              """;
        const string FixedSource = """
                                   public class C
                                   {
                                       private readonly System.Threading.Lock _gate = new();

                                       public void M()
                                       {
                                           lock (_gate)
                                           {
                                           }
                                       }
                                   }
                                   """;
        await VerifyNet90Async(Source, FixedSource);
    }

    /// <summary>Verifies an object field used for anything other than locking is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ObjectUsedBeyondLockingIsCleanAsync()
    {
        const string Source = """
                              public class C
                              {
                                  private readonly object _gate = new();

                                  public void M()
                                  {
                                      lock (_gate)
                                      {
                                      }

                                      System.Console.WriteLine(_gate);
                                  }
                              }
                              """;

        var test = new VerifyLock.Test { ReferenceAssemblies = ReferenceAssemblies.Net.Net90, TestCode = Source, FixedCode = Source };
        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies the rule stays silent where System.Threading.Lock does not exist (pre-.NET 9).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SilentWhenLockTypeUnavailableAsync()
        => await VerifyLock.VerifyAnalyzerAsync(
            """
            public class C
            {
                private readonly object _gate = new();

                public void M()
                {
                    lock (_gate)
                    {
                    }
                }
            }
            """);

    /// <summary>Runs a code-fix verification against the .NET 9 reference assemblies (where the Lock type exists).</summary>
    /// <param name="source">The source with diagnostic markup.</param>
    /// <param name="fixedSource">The expected fixed source.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task VerifyNet90Async(string source, string fixedSource)
    {
        var test = new VerifyLock.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = source,
            FixedCode = fixedSource,
        };

        await test.RunAsync(CancellationToken.None);
    }
}
