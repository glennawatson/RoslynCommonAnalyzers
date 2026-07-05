// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Testing;

using Verify = PerformanceSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    PerformanceSharp.Analyzers.Psh1409ThrowHelperAnalyzer,
    PerformanceSharp.Analyzers.Psh1409ThrowHelperCodeFixProvider>;

namespace PerformanceSharp.Analyzers.Tests;

/// <summary>Tests for <see cref="Psh1409ThrowHelperAnalyzer"/> (PSH1409 throw helpers).</summary>
public class ThrowHelperAnalyzerUnitTest
{
    /// <summary>The polyfill-and-alias sources mirroring the Primitives model on old frameworks.</summary>
    private const string PolyfillSource = """
        namespace Polyfills
        {
            internal static class ArgumentExceptionHelper
            {
                public static void ThrowIfNull(object argument, string paramName = null)
                {
                    if (argument == null)
                    {
                        throw new System.ArgumentNullException(paramName);
                    }
                }
            }
        }
        """;

    /// <summary>The global alias that routes the helper name at every call site.</summary>
    private const string AliasSource = "global using ArgumentExceptionHelper = Polyfills.ArgumentExceptionHelper;";

    /// <summary>Verifies a null guard is flagged and rewritten to ThrowIfNull.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NullGuardIsFlaggedAndFixedAsync()
    {
        const string Source = """
                              using System;

                              public class C
                              {
                                  public void M(string name)
                                  {
                                      {|PSH1409:if (name is null)
                                      {
                                          throw new ArgumentNullException(nameof(name));
                                      }|}
                                  }
                              }
                              """;
        const string FixedSource = """
                                   using System;

                                   public class C
                                   {
                                       public void M(string name)
                                       {
                                           ArgumentNullException.ThrowIfNull(name);
                                       }
                                   }
                                   """;
        await VerifyAsync(Source, FixedSource);
    }

    /// <summary>Verifies an emptiness guard throwing ArgumentException is flagged and rewritten.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NullOrEmptyGuardIsFlaggedAndFixedAsync()
    {
        const string Source = """
                              using System;

                              public class C
                              {
                                  public void M(string name)
                                  {
                                      {|PSH1409:if (string.IsNullOrEmpty(name))
                                      {
                                          throw new ArgumentException("Value required.", nameof(name));
                                      }|}
                                  }
                              }
                              """;
        const string FixedSource = """
                                   using System;

                                   public class C
                                   {
                                       public void M(string name)
                                       {
                                           ArgumentException.ThrowIfNullOrEmpty(name);
                                       }
                                   }
                                   """;
        await VerifyAsync(Source, FixedSource);
    }

    /// <summary>Verifies a disposal guard is flagged and rewritten to ThrowIf with this.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task DisposedGuardIsFlaggedAndFixedAsync()
    {
        const string Source = """
                              using System;

                              public class C
                              {
                                  private bool _disposed;

                                  public void M()
                                  {
                                      {|PSH1409:if (_disposed)
                                      {
                                          throw new ObjectDisposedException(GetType().FullName);
                                      }|}
                                  }
                              }
                              """;
        const string FixedSource = """
                                   using System;

                                   public class C
                                   {
                                       private bool _disposed;

                                       public void M()
                                       {
                                           ObjectDisposedException.ThrowIf(_disposed, this);
                                       }
                                   }
                                   """;
        await VerifyAsync(Source, FixedSource);
    }

    /// <summary>Verifies a negative range guard is flagged and rewritten to ThrowIfNegative.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NegativeGuardIsFlaggedAndFixedAsync()
    {
        const string Source = """
                              using System;

                              public class C
                              {
                                  public void M(int count)
                                  {
                                      {|PSH1409:if (count < 0)
                                      {
                                          throw new ArgumentOutOfRangeException(nameof(count));
                                      }|}
                                  }
                              }
                              """;
        const string FixedSource = """
                                   using System;

                                   public class C
                                   {
                                       public void M(int count)
                                       {
                                           ArgumentOutOfRangeException.ThrowIfNegative(count);
                                       }
                                   }
                                   """;
        await VerifyAsync(Source, FixedSource);
    }

    /// <summary>Verifies a two-operand comparison guard maps to ThrowIfGreaterThan.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task GreaterThanGuardIsFlaggedAndFixedAsync()
    {
        const string Source = """
                              using System;

                              public class C
                              {
                                  public void M(int count, int limit)
                                  {
                                      {|PSH1409:if (count > limit)
                                      {
                                          throw new ArgumentOutOfRangeException(nameof(count));
                                      }|}
                                  }
                              }
                              """;
        const string FixedSource = """
                                   using System;

                                   public class C
                                   {
                                       public void M(int count, int limit)
                                       {
                                           ArgumentOutOfRangeException.ThrowIfGreaterThan(count, limit);
                                       }
                                   }
                                   """;
        await VerifyAsync(Source, FixedSource);
    }

    /// <summary>Verifies a guard naming a different parameter stays clean.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task MismatchedParamNameIsCleanAsync()
        => await VerifyAsync(
            """
            using System;

            public class C
            {
                public void M(string name, string other)
                {
                    if (name is null)
                    {
                        throw new ArgumentNullException(nameof(other));
                    }
                }
            }
            """);

    /// <summary>Verifies a framework without helpers and without aliases stays clean.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task FrameworkWithoutHelpersIsCleanAsync()
    {
        var test = new Verify.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net50,
            TestCode = """
                       using System;

                       public class C
                       {
                           public void M(string name)
                           {
                               if (name is null)
                               {
                                   throw new ArgumentNullException(nameof(name));
                               }
                           }
                       }
                       """,
        };
        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies an aliased polyfill helper makes the guard fixable on frameworks without the BCL helpers.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task AliasedHelperIsUsedOnOldFrameworksAsync()
    {
        const string Source = """
                              using System;

                              public class C
                              {
                                  public void M(string name)
                                  {
                                      {|PSH1409:if (name is null)
                                      {
                                          throw new ArgumentNullException(nameof(name));
                                      }|}
                                  }
                              }
                              """;
        const string FixedSource = """
                                   using System;

                                   public class C
                                   {
                                       public void M(string name)
                                       {
                                           ArgumentExceptionHelper.ThrowIfNull(name);
                                       }
                                   }
                                   """;
        var test = new Verify.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net50,
            TestCode = Source,
            FixedCode = FixedSource,
        };
        test.TestState.Sources.Add(PolyfillSource);
        test.TestState.Sources.Add(AliasSource);
        test.FixedState.Sources.Add(PolyfillSource);
        test.FixedState.Sources.Add(AliasSource);
        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies a null guard on a System.Threading.Lock is left alone — ThrowIfNull would not compile (CS9216).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NullGuardOnLockIsCleanAsync()
        => await VerifyAsync(
            """
            using System;
            using System.Threading;

            public class C
            {
                public void M(Lock gate)
                {
                    if (gate is null)
                    {
                        throw new ArgumentNullException(nameof(gate));
                    }
                }
            }
            """);

    /// <summary>Runs a verification against the .NET 9 reference assemblies.</summary>
    /// <param name="source">The test source.</param>
    /// <param name="fixedSource">The expected fixed source, when a fix should apply.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task VerifyAsync(string source, string? fixedSource = null)
    {
        var test = new Verify.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = source,
        };
        if (fixedSource is not null)
        {
            test.FixedCode = fixedSource;
        }

        await test.RunAsync(CancellationToken.None);
    }
}
