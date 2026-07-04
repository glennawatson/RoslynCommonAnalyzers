// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Testing;

using Verify = PerformanceSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    PerformanceSharp.Analyzers.Psh1309UnsafeRegisterAnalyzer,
    PerformanceSharp.Analyzers.Psh1309UnsafeRegisterCodeFixProvider>;

namespace PerformanceSharp.Analyzers.Tests;

/// <summary>Tests for <see cref="Psh1309UnsafeRegisterAnalyzer"/> (PSH1309 UnsafeRegister, opt-in).</summary>
public class UnsafeRegisterAnalyzerUnitTest
{
    /// <summary>The editorconfig that opts into the disabled-by-default rule.</summary>
    private const string OptInConfig = """
        root = true

        [*.cs]
        dotnet_diagnostic.PSH1309.severity = warning
        """;

    /// <summary>Verifies the callback-and-state Register overload is flagged and renamed.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task RegisterWithStateIsFlaggedAndFixedAsync()
    {
        const string Source = """
                              using System;
                              using System.Threading;

                              public class C
                              {
                                  public void M(CancellationToken token, IDisposable resource)
                                      => {|PSH1309:token.Register(static state => ((IDisposable)state).Dispose(), resource)|};
                              }
                              """;
        const string FixedSource = """
                                   using System;
                                   using System.Threading;

                                   public class C
                                   {
                                       public void M(CancellationToken token, IDisposable resource)
                                           => token.UnsafeRegister(static state => ((IDisposable)state).Dispose(), resource);
                                   }
                                   """;
        await VerifyOptInAsync(Source, FixedSource);
    }

    /// <summary>Verifies the parameterless-callback Register overload stays clean; it has no UnsafeRegister twin.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task RegisterWithoutStateIsCleanAsync()
        => await VerifyOptInAsync(
            """
            using System;
            using System.Threading;

            public class C
            {
                public void M(CancellationToken token)
                    => token.Register(static () => Console.WriteLine());
            }
            """);

    /// <summary>Verifies the rule ships disabled by default; skipping the context capture changes AsyncLocal visibility.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task RuleIsOffByDefaultAsync()
        => await Assert.That(ConcurrencyRules.UseUnsafeRegister.IsEnabledByDefault).IsFalse();

    /// <summary>Runs an opted-in verification against the .NET 9 reference assemblies.</summary>
    /// <param name="source">The test source.</param>
    /// <param name="fixedSource">The expected fixed source, when a fix should apply.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task VerifyOptInAsync(string source, string? fixedSource = null)
    {
        var test = new Verify.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = source,
        };
        test.TestState.AnalyzerConfigFiles.Add(("/.editorconfig", OptInConfig));
        if (fixedSource is not null)
        {
            test.FixedCode = fixedSource;
            test.FixedState.AnalyzerConfigFiles.Add(("/.editorconfig", OptInConfig));
        }

        await test.RunAsync(CancellationToken.None);
    }
}
