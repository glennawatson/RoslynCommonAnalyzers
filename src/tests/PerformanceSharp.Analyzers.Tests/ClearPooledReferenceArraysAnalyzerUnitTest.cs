// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Verify = PerformanceSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    PerformanceSharp.Analyzers.Psh1010ClearPooledReferenceArraysAnalyzer,
    PerformanceSharp.Analyzers.Psh1010ClearPooledReferenceArraysCodeFixProvider>;

namespace PerformanceSharp.Analyzers.Tests;

/// <summary>Tests for <see cref="Psh1010ClearPooledReferenceArraysAnalyzer"/> (PSH1010 pooled reference arrays).</summary>
public class ClearPooledReferenceArraysAnalyzerUnitTest
{
    /// <summary>Verifies returning a reference array without clearing is flagged and the fix adds the flag.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task UnclearedReferenceArrayReturnIsFlaggedAndFixedAsync()
    {
        const string Source = """
                              using System.Buffers;

                              public class C
                              {
                                  public void M(string[] rented)
                                  {
                                      {|PSH1010:ArrayPool<string>.Shared.Return(rented)|};
                                  }
                              }
                              """;
        const string FixedSource = """
                                   using System.Buffers;

                                   public class C
                                   {
                                       public void M(string[] rented)
                                       {
                                           ArrayPool<string>.Shared.Return(rented, clearArray: true);
                                       }
                                   }
                                   """;
        await Verify.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies an already-cleared return is clean.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ClearedReturnIsCleanAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            using System.Buffers;

            public class C
            {
                public void M(string[] rented)
                {
                    ArrayPool<string>.Shared.Return(rented, true);
                }
            }
            """);

    /// <summary>Verifies an explicit false flag is flagged and substituted by the fix.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ExplicitFalseFlagIsFlaggedAndFixedAsync()
    {
        const string Source = """
                              using System.Buffers;

                              public class C
                              {
                                  public void M(string[] rented)
                                  {
                                      {|PSH1010:ArrayPool<string>.Shared.Return(rented, false)|};
                                  }
                              }
                              """;
        const string FixedSource = """
                                   using System.Buffers;

                                   public class C
                                   {
                                       public void M(string[] rented)
                                       {
                                           ArrayPool<string>.Shared.Return(rented, true);
                                       }
                                   }
                                   """;
        await Verify.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies unmanaged element types are clean; nothing is kept alive.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task UnmanagedElementTypeIsCleanAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            using System.Buffers;

            public class C
            {
                public void M(byte[] rented)
                {
                    ArrayPool<byte>.Shared.Return(rented);
                }
            }
            """);

    /// <summary>Verifies a struct holding a reference field is flagged like a reference type.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ReferenceCarryingStructIsFlaggedAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            using System.Buffers;

            public struct Entry
            {
                public string Name;
            }

            public class C
            {
                public void M(Entry[] rented)
                {
                    {|PSH1010:ArrayPool<Entry>.Shared.Return(rented)|};
                }
            }
            """);

    /// <summary>Verifies an unconstrained type parameter stays clean; the element may be unmanaged.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task UnconstrainedTypeParameterIsCleanAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            using System.Buffers;

            public class C
            {
                public void M<T>(T[] rented)
                {
                    ArrayPool<T>.Shared.Return(rented);
                }
            }
            """);

    /// <summary>Verifies a class-constrained type parameter is flagged.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ClassConstrainedTypeParameterIsFlaggedAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            using System.Buffers;

            public class C
            {
                public void M<T>(T[] rented)
                    where T : class
                {
                    {|PSH1010:ArrayPool<T>.Shared.Return(rented)|};
                }
            }
            """);
}
