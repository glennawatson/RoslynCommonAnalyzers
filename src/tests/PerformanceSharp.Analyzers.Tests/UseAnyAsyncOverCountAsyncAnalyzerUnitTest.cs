// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Testing;

using VerifyAnyAsync = PerformanceSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    PerformanceSharp.Analyzers.Psh1126UseAnyAsyncOverCountAsyncAnalyzer,
    PerformanceSharp.Analyzers.Psh1126UseAnyAsyncOverCountAsyncCodeFixProvider>;

namespace PerformanceSharp.Analyzers.Tests;

/// <summary>Unit tests for PSH1126 (ask an async sequence for elements without counting them) and its code fix.</summary>
public class UseAnyAsyncOverCountAsyncAnalyzerUnitTest
{
    /// <summary>A stand-in async query provider, mirroring the extension-method shape real providers ship.</summary>
    private const string Provider = """
                                    using System.Linq;
                                    using System.Threading;
                                    using System.Threading.Tasks;

                                    public static class AsyncQuery
                                    {
                                        public static Task<int> CountAsync<T>(this IQueryable<T> source, CancellationToken cancellationToken = default) => Task.FromResult(0);

                                        public static Task<bool> AnyAsync<T>(this IQueryable<T> source, CancellationToken cancellationToken = default) => Task.FromResult(false);
                                    }

                                    """;

    /// <summary>A provider that can count but has no AnyAsync sibling to move to.</summary>
    private const string ProviderWithoutAny = """
                                              using System.Linq;
                                              using System.Threading;
                                              using System.Threading.Tasks;

                                              public static class AsyncQuery
                                              {
                                                  public static Task<int> CountAsync<T>(this IQueryable<T> source, CancellationToken cancellationToken = default) => Task.FromResult(0);
                                              }

                                              """;

    /// <summary>Verifies an awaited CountAsync() &gt; 0 is reported and rewritten to AnyAsync().</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task CountAsyncGreaterThanZeroReplacedWithAnyAsync()
    {
        const string Body = """
                            public class C
                            {
                                public async Task<bool> M(IQueryable<int> query) => {|PSH1126:await query.CountAsync() > 0|};
                            }
                            """;
        const string FixedBody = """
                                 public class C
                                 {
                                     public async Task<bool> M(IQueryable<int> query) => await query.AnyAsync();
                                 }
                                 """;
        await VerifyNet90Async(Provider + Body, Provider + FixedBody);
    }

    /// <summary>Verifies an awaited CountAsync() == 0 is reported and rewritten to a negated AnyAsync().</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task CountAsyncEqualsZeroReplacedWithNegatedAnyAsync()
    {
        const string Body = """
                            public class C
                            {
                                public async Task<bool> M(IQueryable<int> query) => {|PSH1126:await query.CountAsync() == 0|};
                            }
                            """;
        const string FixedBody = """
                                 public class C
                                 {
                                     public async Task<bool> M(IQueryable<int> query) => !await query.AnyAsync();
                                 }
                                 """;
        await VerifyNet90Async(Provider + Body, Provider + FixedBody);
    }

    /// <summary>Verifies the reversed operand order is reported and rewritten.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ReversedOperandOrderReplacedWithAnyAsync()
    {
        const string Body = """
                            public class C
                            {
                                public async Task<bool> M(IQueryable<int> query) => {|PSH1126:0 < await query.CountAsync()|};
                            }
                            """;
        const string FixedBody = """
                                 public class C
                                 {
                                     public async Task<bool> M(IQueryable<int> query) => await query.AnyAsync();
                                 }
                                 """;
        await VerifyNet90Async(Provider + Body, Provider + FixedBody);
    }

    /// <summary>Verifies a cancellation-token argument is carried over to the AnyAsync call.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task CancellationTokenArgumentIsCarriedOverAsync()
    {
        const string Body = """
                            public class C
                            {
                                public async Task<bool> M(IQueryable<int> query, CancellationToken token)
                                    => {|PSH1126:await query.CountAsync(token) > 0|};
                            }
                            """;
        const string FixedBody = """
                                 public class C
                                 {
                                     public async Task<bool> M(IQueryable<int> query, CancellationToken token)
                                         => await query.AnyAsync(token);
                                 }
                                 """;
        await VerifyNet90Async(Provider + Body, Provider + FixedBody);
    }

    /// <summary>Verifies a provider with no AnyAsync sibling is never reported, so no unfixable diagnostic appears.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ProviderWithoutAnyAsyncIsNotReportedAsync()
    {
        const string Body = """
                            public class C
                            {
                                public async Task<bool> M(IQueryable<int> query) => await query.CountAsync() > 0;
                            }
                            """;
        await VerifyNet90Async(ProviderWithoutAny + Body, ProviderWithoutAny + Body);
    }

    /// <summary>Verifies a comparison that needs the real count is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ComparisonAgainstNonZeroIsNotReportedAsync()
    {
        const string Body = """
                            public class C
                            {
                                public async Task<bool> M(IQueryable<int> query) => await query.CountAsync() > 5;
                            }
                            """;
        await VerifyNet90Async(Provider + Body, Provider + Body);
    }

    /// <summary>Runs a code-fix verification against the .NET 9 reference assemblies.</summary>
    /// <param name="source">The source with diagnostic markup.</param>
    /// <param name="fixedSource">The expected fixed source.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task VerifyNet90Async(string source, string fixedSource)
    {
        var test = new VerifyAnyAsync.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = source,
            FixedCode = fixedSource
        };

        await test.RunAsync(CancellationToken.None);
    }
}
