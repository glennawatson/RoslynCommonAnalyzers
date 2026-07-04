// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Testing;

using Verify = PerformanceSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    PerformanceSharp.Analyzers.Psh1012EqualityComparerDefaultAnalyzer,
    PerformanceSharp.Analyzers.Psh1012EqualityComparerDefaultCodeFixProvider>;

namespace PerformanceSharp.Analyzers.Tests;

/// <summary>Tests for <see cref="Psh1012EqualityComparerDefaultAnalyzer"/> (PSH1012 type parameter equality).</summary>
public class EqualityComparerDefaultAnalyzerUnitTest
{
    /// <summary>Verifies an instance Equals on an unconstrained type parameter is flagged and rewritten.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task InstanceEqualsOnTypeParameterIsFlaggedAndFixedAsync()
    {
        const string Source = """
                              using System.Collections.Generic;

                              public class C
                              {
                                  public bool M<T>(T current, T next) => {|PSH1012:current.Equals(next)|};
                              }
                              """;
        const string FixedSource = """
                                   using System.Collections.Generic;

                                   public class C
                                   {
                                       public bool M<T>(T current, T next) => EqualityComparer<T>.Default.Equals(current, next);
                                   }
                                   """;
        await VerifyAsync(Source, FixedSource);
    }

    /// <summary>Verifies the static object.Equals over type parameters is flagged and rewritten.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task StaticEqualsOnTypeParameterIsFlaggedAndFixedAsync()
    {
        const string Source = """
                              using System.Collections.Generic;

                              public class C
                              {
                                  public bool M<T>(T current, T next) => {|PSH1012:Equals(current, next)|};
                              }
                              """;
        const string FixedSource = """
                                   using System.Collections.Generic;

                                   public class C
                                   {
                                       public bool M<T>(T current, T next) => EqualityComparer<T>.Default.Equals(current, next);
                                   }
                                   """;
        await VerifyAsync(Source, FixedSource);
    }

    /// <summary>Verifies an equatable-constrained type parameter stays clean; its Equals never boxes.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task EquatableConstrainedTypeParameterIsCleanAsync()
        => await VerifyAsync(
            """
            using System;

            public class C
            {
                public bool M<T>(T current, T next)
                    where T : IEquatable<T>
                    => current.Equals(next);
            }
            """);

    /// <summary>Verifies a reference-constrained type parameter stays clean; nothing boxes.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ClassConstrainedTypeParameterIsCleanAsync()
        => await VerifyAsync(
            """
            public class C
            {
                public bool M<T>(T current, T next)
                    where T : class
                    => current.Equals(next);
            }
            """);

    /// <summary>Verifies an ordinary object comparison stays clean; only type parameters are reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ObjectComparisonIsCleanAsync()
        => await VerifyAsync(
            """
            public class C
            {
                public bool M(object current, object next) => current.Equals(next);
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
