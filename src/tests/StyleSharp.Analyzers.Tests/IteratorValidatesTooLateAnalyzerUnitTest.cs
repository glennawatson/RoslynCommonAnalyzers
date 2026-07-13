// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Testing;

using VerifyIteratorGuard = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.Sst2404IteratorValidatesTooLateAnalyzer,
    StyleSharp.Analyzers.Sst2404IteratorValidatesTooLateCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST2404 (an iterator whose argument guards do not run until it is enumerated) and its fix.</summary>
public class IteratorValidatesTooLateAnalyzerUnitTest
{
    /// <summary>Verifies a guarded iterator is reported and split into a validating wrapper and an iterator.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task GuardedIteratorIsSplitAsync()
    {
        const string Source = """
                              using System;
                              using System.Collections.Generic;

                              public sealed class C
                              {
                                  public IEnumerable<int> {|SST2404:Doubled|}(int[] values)
                                  {
                                      if (values is null)
                                      {
                                          throw new ArgumentNullException(nameof(values));
                                      }

                                      foreach (var value in values)
                                      {
                                          yield return value * 2;
                                      }
                                  }
                              }
                              """;
        const string FixedSource = """
                                   using System;
                                   using System.Collections.Generic;

                                   public sealed class C
                                   {
                                       public IEnumerable<int> Doubled(int[] values)
                                       {
                                           if (values is null)
                                           {
                                               throw new ArgumentNullException(nameof(values));
                                           }

                                           return Iterator();

                                           IEnumerable<int> Iterator()
                                           {
                                               foreach (var value in values)
                                               {
                                                   yield return value * 2;
                                               }
                                           }
                                       }
                                   }
                                   """;
        await VerifyIteratorGuard.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a throw-helper guard is recognized, and that every leading guard moves with it.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task EveryLeadingGuardMovesAsync()
    {
        const string Source = """
                              using System;
                              using System.Collections.Generic;

                              public sealed class C
                              {
                                  public IEnumerable<int> {|SST2404:Take|}(int[] values, int count)
                                  {
                                      ArgumentNullException.ThrowIfNull(values);
                                      ArgumentOutOfRangeException.ThrowIfNegative(count);

                                      for (var i = 0; i < count; i++)
                                      {
                                          yield return values[i];
                                      }
                                  }
                              }
                              """;
        const string FixedSource = """
                                   using System;
                                   using System.Collections.Generic;

                                   public sealed class C
                                   {
                                       public IEnumerable<int> Take(int[] values, int count)
                                       {
                                           ArgumentNullException.ThrowIfNull(values);
                                           ArgumentOutOfRangeException.ThrowIfNegative(count);

                                           return Iterator();

                                           IEnumerable<int> Iterator()
                                           {
                                               for (var i = 0; i < count; i++)
                                               {
                                                   yield return values[i];
                                               }
                                           }
                                       }
                                   }
                                   """;
        await VerifyNet80CodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a generic iterator splits without a signature change, the type parameter staying in scope.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task GenericIteratorIsSplitAsync()
    {
        const string Source = """
                              using System;
                              using System.Collections.Generic;

                              public sealed class C
                              {
                                  public IEnumerable<T> {|SST2404:Repeat|}<T>(T value, int times)
                                  {
                                      if (times < 0)
                                      {
                                          throw new ArgumentOutOfRangeException(nameof(times));
                                      }

                                      for (var i = 0; i < times; i++)
                                      {
                                          yield return value;
                                      }
                                  }
                              }
                              """;
        const string FixedSource = """
                                   using System;
                                   using System.Collections.Generic;

                                   public sealed class C
                                   {
                                       public IEnumerable<T> Repeat<T>(T value, int times)
                                       {
                                           if (times < 0)
                                           {
                                               throw new ArgumentOutOfRangeException(nameof(times));
                                           }

                                           return Iterator();

                                           IEnumerable<T> Iterator()
                                           {
                                               for (var i = 0; i < times; i++)
                                               {
                                                   yield return value;
                                               }
                                           }
                                       }
                                   }
                                   """;
        await VerifyIteratorGuard.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies the extracted iterator takes a name nothing in the method already uses.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task IteratorNameAvoidsACollisionAsync()
    {
        const string Source = """
                              using System;
                              using System.Collections.Generic;

                              public sealed class C
                              {
                                  public IEnumerable<int> {|SST2404:Doubled|}(int[] values)
                                  {
                                      if (values is null)
                                      {
                                          throw new ArgumentNullException(nameof(values));
                                      }

                                      var Iterator = values.Length;
                                      yield return Iterator;
                                  }
                              }
                              """;
        const string FixedSource = """
                                   using System;
                                   using System.Collections.Generic;

                                   public sealed class C
                                   {
                                       public IEnumerable<int> Doubled(int[] values)
                                       {
                                           if (values is null)
                                           {
                                               throw new ArgumentNullException(nameof(values));
                                           }

                                           return Iterator2();

                                           IEnumerable<int> Iterator2()
                                           {
                                               var Iterator = values.Length;
                                               yield return Iterator;
                                           }
                                       }
                                   }
                                   """;
        await VerifyIteratorGuard.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies an async iterator is reported but not rewritten.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    /// <remarks>
    /// Its cancellation token is bound to the iterator by an attribute on the parameter, and moving the body
    /// into a local function would quietly stop that token being honoured.
    /// </remarks>
    [Test]
    public async Task AsyncIteratorIsReportedButNotFixedAsync()
    {
        const string Source = """
                              using System;
                              using System.Collections.Generic;
                              using System.Threading.Tasks;

                              public sealed class C
                              {
                                  public async IAsyncEnumerable<int> {|SST2404:Doubled|}(int[] values)
                                  {
                                      if (values is null)
                                      {
                                          throw new ArgumentNullException(nameof(values));
                                      }

                                      foreach (var value in values)
                                      {
                                          await Task.Yield();
                                          yield return value * 2;
                                      }
                                  }
                              }
                              """;
        await VerifyIteratorGuard.VerifyCodeFixAsync(Source, Source);
    }

    /// <summary>Verifies an iterator with no guard is clean.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task UnguardedIteratorIsCleanAsync()
        => await VerifyIteratorGuard.VerifyAnalyzerAsync(
            """
            using System.Collections.Generic;

            public sealed class C
            {
                public IEnumerable<int> Doubled(int[] values)
                {
                    foreach (var value in values)
                    {
                        yield return value * 2;
                    }
                }
            }
            """);

    /// <summary>Verifies a guarded method that is not an iterator is clean: its body runs when it is called.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task GuardedNonIteratorIsCleanAsync()
        => await VerifyNet80AnalyzerAsync(
            """
            using System;
            using System.Collections.Generic;

            public sealed class C
            {
                public IEnumerable<int> Doubled(int[] values)
                {
                    ArgumentNullException.ThrowIfNull(values);
                    return new List<int>(values);
                }
            }
            """);

    /// <summary>Verifies a method already split into a wrapper and an iterator is clean.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task AlreadySplitMethodIsCleanAsync()
        => await VerifyNet80AnalyzerAsync(
            """
            using System;
            using System.Collections.Generic;

            public sealed class C
            {
                public IEnumerable<int> Doubled(int[] values)
                {
                    ArgumentNullException.ThrowIfNull(values);

                    return Iterator();

                    IEnumerable<int> Iterator()
                    {
                        foreach (var value in values)
                        {
                            yield return value * 2;
                        }
                    }
                }
            }
            """);

    /// <summary>Verifies an iterator whose leading guard checks its own state, not an argument, is clean.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task StateGuardWithoutAnArgumentCheckIsCleanAsync()
        => await VerifyIteratorGuard.VerifyAnalyzerAsync(
            """
            using System;
            using System.Collections.Generic;

            public sealed class C
            {
                private bool _disposed;

                public IEnumerable<int> Doubled(int[] values)
                {
                    if (_disposed)
                    {
                        throw new ObjectDisposedException(nameof(C));
                    }

                    foreach (var value in values)
                    {
                        yield return value * 2;
                    }
                }

                public void Dispose() => _disposed = true;
            }
            """);

    /// <summary>Runs an analyzer verification against the .NET 8 reference assemblies, where the throw-helpers exist.</summary>
    /// <param name="source">The source with diagnostic markup.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task VerifyNet80AnalyzerAsync(string source)
    {
        var test = new VerifyIteratorGuard.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
            TestCode = source,
        };

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Runs a code-fix verification against the .NET 8 reference assemblies, where the throw-helpers exist.</summary>
    /// <param name="source">The source with diagnostic markup.</param>
    /// <param name="fixedSource">The expected fixed source.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task VerifyNet80CodeFixAsync(string source, string fixedSource)
    {
        var test = new VerifyIteratorGuard.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
            TestCode = source,
            FixedCode = fixedSource,
        };

        await test.RunAsync(CancellationToken.None);
    }
}
