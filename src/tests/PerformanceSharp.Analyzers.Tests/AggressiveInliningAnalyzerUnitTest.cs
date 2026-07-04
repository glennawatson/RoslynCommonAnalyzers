// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Testing;

using Verify = PerformanceSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    PerformanceSharp.Analyzers.Psh1410AggressiveInliningAnalyzer,
    PerformanceSharp.Analyzers.Psh1410AggressiveInliningCodeFixProvider>;

namespace PerformanceSharp.Analyzers.Tests;

/// <summary>Tests for <see cref="Psh1410AggressiveInliningAnalyzer"/> (PSH1410 aggressive inlining, opt-in).</summary>
public class AggressiveInliningAnalyzerUnitTest
{
    /// <summary>The editorconfig that opts into the disabled-by-default rule.</summary>
    private const string OptInConfig = """
        root = true

        [*.cs]
        dotnet_diagnostic.PSH1410.severity = warning
        """;

    /// <summary>Verifies a trivial forwarder is flagged and gains the attribute.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task TrivialForwarderIsFlaggedAndFixedAsync()
    {
        const string Source = """
                              using System.Runtime.CompilerServices;

                              public class C
                              {
                                  private readonly int _value;

                                  public C(int value) => _value = value;

                                  public int {|PSH1410:GetValue|}() => _value;
                              }
                              """;
        const string FixedSource = """
                                   using System.Runtime.CompilerServices;

                                   public class C
                                   {
                                       private readonly int _value;

                                       public C(int value) => _value = value;

                                       [MethodImpl(MethodImplOptions.AggressiveInlining)]
                                       public int GetValue() => _value;
                                   }
                                   """;
        await VerifyOptInAsync(Source, FixedSource);
    }

    /// <summary>Verifies a member that already carries a MethodImpl attribute stays clean.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ExistingMethodImplIsCleanAsync()
        => await VerifyOptInAsync(
            """
            using System.Runtime.CompilerServices;

            public class C
            {
                [MethodImpl(MethodImplOptions.NoInlining)]
                public int GetValue() => 42;
            }
            """);

    /// <summary>Verifies a virtual member stays clean.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task VirtualMemberIsCleanAsync()
        => await VerifyOptInAsync(
            """
            public class C
            {
                public virtual int GetValue() => 42;
            }
            """);

    /// <summary>Verifies a block-bodied method stays clean; only expression bodies are forwarders.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task BlockBodiedMethodIsCleanAsync()
        => await VerifyOptInAsync(
            """
            public class C
            {
                public int GetValue()
                {
                    return 42;
                }
            }
            """);

    /// <summary>Verifies the rule ships disabled by default; blanket inlining is an opinionated convention.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task RuleIsOffByDefaultAsync()
        => await Assert.That(ApiSelectionRules.InlineTrivialForwarders.IsEnabledByDefault).IsFalse();

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
