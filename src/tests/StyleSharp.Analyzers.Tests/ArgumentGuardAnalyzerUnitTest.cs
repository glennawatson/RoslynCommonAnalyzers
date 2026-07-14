// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
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

    /// <summary>Verifies an IsNullOrEmpty guard is reported (SST2001) and rewritten to ThrowIfNullOrEmpty.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task IsNullOrEmptyGuardReplacedAsync()
    {
        const string Source = """
                              using System;

                              public class C
                              {
                                  public void M(string value)
                                  {
                                      {|SST2001:if (string.IsNullOrEmpty(value)) throw new ArgumentException("Value required.", nameof(value));|}
                                  }
                              }
                              """;
        const string FixedSource = """
                                   using System;

                                   public class C
                                   {
                                       public void M(string value)
                                       {
                                           ArgumentException.ThrowIfNullOrEmpty(value);
                                       }
                                   }
                                   """;
        await VerifyNet80Async(Source, FixedSource);
    }

    /// <summary>Verifies an IsNullOrWhiteSpace guard is reported (SST2002) and rewritten to ThrowIfNullOrWhiteSpace.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task IsNullOrWhiteSpaceGuardReplacedAsync()
    {
        const string Source = """
                              using System;

                              public class C
                              {
                                  public void M(string value)
                                  {
                                      {|SST2002:if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("Value required.", nameof(value));|}
                                  }
                              }
                              """;
        const string FixedSource = """
                                   using System;

                                   public class C
                                   {
                                       public void M(string value)
                                       {
                                           ArgumentException.ThrowIfNullOrWhiteSpace(value);
                                       }
                                   }
                                   """;
        await VerifyNet80Async(Source, FixedSource);
    }

    /// <summary>Verifies a standard disposed guard is replaced by ObjectDisposedException.ThrowIf.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task DisposedGuardReplacedAsync()
    {
        const string Source = """
                              using System;

                              public class C
                              {
                                  private bool _disposed;

                                  public void M()
                                  {
                                      {|SST2003:if (_disposed) throw new ObjectDisposedException(nameof(C));|}
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
        await VerifyNet80Async(Source, FixedSource);
    }

    /// <summary>Verifies a negative range guard is replaced by ThrowIfNegative.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NegativeRangeGuardReplacedAsync()
    {
        const string Source = """
                              using System;

                              public class C
                              {
                                  public void M(int value)
                                  {
                                      {|SST2004:if (value < 0) throw new ArgumentOutOfRangeException(nameof(value));|}
                                  }
                              }
                              """;
        const string FixedSource = """
                                   using System;

                                   public class C
                                   {
                                       public void M(int value)
                                       {
                                           ArgumentOutOfRangeException.ThrowIfNegative(value);
                                       }
                                   }
                                   """;
        await VerifyNet80Async(Source, FixedSource);
    }

    /// <summary>Verifies an ambiguous range comparison is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task AmbiguousRangeGuardIsCleanAsync()
    {
        const string Source = """
                              using System;

                              public class C
                              {
                                  public void M(int value, int other)
                                  {
                                      if (other < 0) throw new ArgumentOutOfRangeException(nameof(value));
                                  }
                              }
                              """;
        await VerifyNet80Async(Source, Source);
    }

    /// <summary>Verifies Fix All rewrites every guard clause in a single document in one pass.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task FixAllRewritesEveryOccurrenceAsync()
    {
        const string Source = """
                              using System;

                              public class C
                              {
                                  public void A(object value)
                                  {
                                      {|SST2000:if (value is null) throw new ArgumentNullException(nameof(value));|}
                                  }

                                  public void B(object value)
                                  {
                                      {|SST2000:if (value == null) throw new ArgumentNullException(nameof(value));|}
                                  }

                                  public void D(int value)
                                  {
                                      {|SST2004:if (value < 0) throw new ArgumentOutOfRangeException(nameof(value));|}
                                  }
                              }
                              """;
        const string FixedSource = """
                                   using System;

                                   public class C
                                   {
                                       public void A(object value)
                                       {
                                           ArgumentNullException.ThrowIfNull(value);
                                       }

                                       public void B(object value)
                                       {
                                           ArgumentNullException.ThrowIfNull(value);
                                       }

                                       public void D(int value)
                                       {
                                           ArgumentOutOfRangeException.ThrowIfNegative(value);
                                       }
                                   }
                                   """;
        await VerifyNet80Async(Source, FixedSource);
    }

    /// <summary>Verifies the rule stays silent where ThrowIfNull does not exist (pre-.NET 6 reference assemblies).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    /// <remarks>
    /// The reference set is named rather than left to the verifier's default. <c>ArgumentNullException.ThrowIfNull</c>
    /// arrived in .NET 6, so the framework this runs against is the whole subject of the test; a default that moves
    /// with the testing package would turn it into an assertion about nothing.
    /// </remarks>
    [Test]
    public async Task SilentWhenHelperUnavailableAsync()
    {
        var test = new VerifyGuard.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.NetStandard.NetStandard20,
            TestCode = """
                       using System;

                       public class C
                       {
                           public void M(object value)
                           {
                               if (value is null) throw new ArgumentNullException(nameof(value));
                           }
                       }
                       """,
        };

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies helper availability can be resolved from a compilation in one place.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task CreateHelpersFindsCurrentRuntimeThrowHelpersAsync()
    {
        var compilation = CreateCompilation();
        var helpers = ArgumentGuardAnalyzer.CreateHelpers(compilation);

        await Assert.That(helpers.ThrowIfNull).IsTrue();
        await Assert.That(helpers.Any).IsTrue();
    }

    /// <summary>Verifies the ArgumentException helper scan can resolve both string throw helpers in one pass.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ReadArgumentExceptionHelpersFindsBothStringHelpersAsync()
    {
        var compilation = CreateCompilation();
        var argumentException = compilation.GetTypeByMetadataName("System.ArgumentException");

        ArgumentGuardAnalyzer.ReadArgumentExceptionHelpers(argumentException, out var throwIfNullOrEmpty, out var throwIfNullOrWhiteSpace);

        await Assert.That(throwIfNullOrEmpty).IsTrue();
        await Assert.That(throwIfNullOrWhiteSpace).IsTrue();
    }

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
            FixedCode = fixedSource
        };

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Creates a minimal compilation against the current runtime reference set.</summary>
    /// <returns>The compilation.</returns>
    private static CSharpCompilation CreateCompilation()
    {
        var trustedAssemblies = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator);
        var references = new MetadataReference[trustedAssemblies.Length];
        for (var i = 0; i < trustedAssemblies.Length; i++)
        {
            references[i] = MetadataReference.CreateFromFile(trustedAssemblies[i]);
        }

        return CSharpCompilation.Create("Bench", references: references);
    }
}
