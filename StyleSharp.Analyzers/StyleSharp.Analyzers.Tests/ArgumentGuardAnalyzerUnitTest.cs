// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Testing;

using VerifyGuard = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.ArgumentGuardAnalyzer,
    StyleSharp.Analyzers.ArgumentGuardCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST2000 (use ArgumentNullException.ThrowIfNull) and its code fix.</summary>
public class ArgumentGuardAnalyzerUnitTest
{
    /// <summary>Verifies an <c>is null</c> guard is reported (SST2000) and rewritten to ThrowIfNull.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task IsNullGuardReplacedAsync()
    {
        const string Source = """
                              using System;

                              public class C
                              {
                                  public void M(object value)
                                  {
                                      {|SST2000:if (value is null) throw new ArgumentNullException(nameof(value));|}
                                  }
                              }
                              """;
        const string FixedSource = """
                                   using System;

                                   public class C
                                   {
                                       public void M(object value)
                                       {
                                           ArgumentNullException.ThrowIfNull(value);
                                       }
                                   }
                                   """;
        await VerifyNet80Async(Source, FixedSource);
    }

    /// <summary>Verifies an <c>== null</c> guard with a block body is reported and rewritten.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task EqualsNullBlockGuardReplacedAsync()
    {
        const string Source = """
                              using System;

                              public class C
                              {
                                  public void M(object value)
                                  {
                                      {|SST2000:if (value == null)
                                      {
                                          throw new ArgumentNullException(nameof(value));
                                      }|}
                                  }
                              }
                              """;
        const string FixedSource = """
                                   using System;

                                   public class C
                                   {
                                       public void M(object value)
                                       {
                                           ArgumentNullException.ThrowIfNull(value);
                                       }
                                   }
                                   """;
        await VerifyNet80Async(Source, FixedSource);
    }

    /// <summary>Verifies an existing ThrowIfNull call and a guard carrying a custom message are not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task AlreadyModernOrCustomMessageIsCleanAsync()
    {
        const string Source = """
                              using System;

                              public class C
                              {
                                  public void M(object value)
                                  {
                                      ArgumentNullException.ThrowIfNull(value);
                                  }

                                  public void N(object value)
                                  {
                                      if (value is null) throw new ArgumentNullException(nameof(value), "must not be null");
                                  }
                              }
                              """;
        await VerifyNet80Async(Source, Source);
    }

    /// <summary>Verifies the rule stays silent where ThrowIfNull does not exist (pre-.NET 6 reference assemblies).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SilentWhenHelperUnavailableAsync()
        => await VerifyGuard.VerifyAnalyzerAsync(
            """
            using System;

            public class C
            {
                public void M(object value)
                {
                    if (value is null) throw new ArgumentNullException(nameof(value));
                }
            }
            """);

    /// <summary>Runs a code-fix verification against the .NET 8 reference assemblies (where the helper exists).</summary>
    /// <param name="source">The source with diagnostic markup.</param>
    /// <param name="fixedSource">The expected fixed source.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task VerifyNet80Async(string source, string fixedSource)
    {
        var test = new VerifyGuard.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
            TestCode = source,
            FixedCode = fixedSource,
        };

        await test.RunAsync(CancellationToken.None);
    }
}
