// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Testing;

using Verify = PerformanceSharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    PerformanceSharp.Analyzers.Psh1306InterlockedOnceGuardAnalyzer>;

namespace PerformanceSharp.Analyzers.Tests;

/// <summary>Tests for <see cref="Psh1306InterlockedOnceGuardAnalyzer"/> (PSH1306 interlocked once-guards, opt-in).</summary>
public class InterlockedOnceGuardAnalyzerUnitTest
{
    /// <summary>The editorconfig that opts into the disabled-by-default rule.</summary>
    private const string OptInConfig = """
        root = true

        [*.cs]
        dotnet_diagnostic.PSH1306.severity = warning
        """;

    /// <summary>Verifies the classic dispose once-guard is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task DisposeOnceGuardIsReportedAsync()
        => await VerifyOptInAsync(
            """
            using System;

            public class C : IDisposable
            {
                private bool _disposed;

                public void Dispose()
                {
                    if ({|PSH1306:_disposed|})
                    {
                        return;
                    }

                    _disposed = true;
                }
            }
            """);

    /// <summary>Verifies a this-qualified guard is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ThisQualifiedGuardIsReportedAsync()
        => await VerifyOptInAsync(
            """
            public class C
            {
                private bool _started;

                public void Start()
                {
                    if ({|PSH1306:this._started|})
                    {
                        return;
                    }

                    this._started = true;
                }
            }
            """);

    /// <summary>Verifies a guard whose check and write sit inside a lock stays clean.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task LockedGuardIsCleanAsync()
        => await VerifyOptInAsync(
            """
            public class C
            {
                private readonly object _gate = new object();
                private bool _started;

                public void Start()
                {
                    lock (_gate)
                    {
                        if (_started)
                        {
                            return;
                        }

                        _started = true;
                    }
                }
            }
            """);

    /// <summary>Verifies a guard over a local variable stays clean.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task LocalFlagIsCleanAsync()
        => await VerifyOptInAsync(
            """
            public class C
            {
                public void M(bool done)
                {
                    if (done)
                    {
                        return;
                    }

                    done = true;
                }
            }
            """);

    /// <summary>Verifies a guard that never writes the flag stays clean.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ReadOnlyGuardIsCleanAsync()
        => await VerifyOptInAsync(
            """
            using System;

            public class C
            {
                private bool _disposed;

                public void M()
                {
                    if (_disposed)
                    {
                        return;
                    }

                    Console.WriteLine();
                }
            }
            """);

    /// <summary>Verifies the rule ships disabled by default; guard thread-safety is contextual.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task RuleIsOffByDefaultAsync()
        => await Assert.That(ConcurrencyRules.InterlockedOnceGuard.IsEnabledByDefault).IsFalse();

    /// <summary>Runs an opted-in verification against the .NET 9 reference assemblies.</summary>
    /// <param name="source">The test source.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task VerifyOptInAsync(string source)
    {
        var test = new Verify.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = source,
        };
        test.TestState.AnalyzerConfigFiles.Add(("/.editorconfig", OptInConfig));
        await test.RunAsync(CancellationToken.None);
    }
}
