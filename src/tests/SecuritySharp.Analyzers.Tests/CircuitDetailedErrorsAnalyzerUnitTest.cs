// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Testing;

using AnalyzeDetailedErrors = SecuritySharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    SecuritySharp.Analyzers.Ses1708CircuitDetailedErrorsAnalyzer>;

namespace SecuritySharp.Analyzers.Tests;

/// <summary>Unit tests for SES1708 (circuit detailed errors must not be enabled outside development).</summary>
public class CircuitDetailedErrorsAnalyzerUnitTest
{
    /// <summary>Inline stub of the server-side Blazor circuit options type carrying the <c>DetailedErrors</c> flag.</summary>
    private const string CircuitOptionsStub = """

                                              namespace Microsoft.AspNetCore.Components.Server
                                              {
                                                  public sealed class CircuitOptions
                                                  {
                                                      public bool DetailedErrors { get; set; }
                                                  }
                                              }
                                              """;

    /// <summary>Verifies a direct <c>DetailedErrors = true</c> assignment is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task DirectAssignmentReportedAsync()
        => await VerifyAsync(
            """
            using Microsoft.AspNetCore.Components.Server;

            public class C
            {
                public void M(CircuitOptions options)
                {
                    {|SES1708:options.DetailedErrors = true|};
                }
            }
            """);

    /// <summary>Verifies the object-initializer form of <c>DetailedErrors = true</c> is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ObjectInitializerReportedAsync()
        => await VerifyAsync(
            """
            using Microsoft.AspNetCore.Components.Server;

            public class C
            {
                public CircuitOptions M()
                    => new CircuitOptions { {|SES1708:DetailedErrors = true|} };
            }
            """);

    /// <summary>Verifies setting <c>DetailedErrors</c> to false is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SetToFalseIsCleanAsync()
        => await VerifyAsync(
            """
            using Microsoft.AspNetCore.Components.Server;

            public class C
            {
                public void M(CircuitOptions options)
                {
                    options.DetailedErrors = false;
                }
            }
            """);

    /// <summary>Verifies a same-named flag on an unrelated type is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SameNamedPropertyOnUnrelatedTypeIsCleanAsync()
        => await VerifyAsync(
            """
            public sealed class MyOptions
            {
                public bool DetailedErrors { get; set; }
            }

            public class C
            {
                public void M(MyOptions options)
                {
                    options.DetailedErrors = true;
                }
            }
            """);

    /// <summary>Verifies the rule stays silent when the circuit-options type is absent from the compilation.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SilentWhenCircuitOptionsUnavailableAsync()
    {
        const string Source = """
                              public sealed class CircuitOptions
                              {
                                  public bool DetailedErrors { get; set; }
                              }

                              public class C
                              {
                                  public void M(CircuitOptions options)
                                  {
                                      options.DetailedErrors = true;
                                  }
                              }
                              """;

        var test = new AnalyzeDetailedErrors.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = Source
        };

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Runs an analyzer-only verification with the inline circuit-options stub appended.</summary>
    /// <param name="source">The source with diagnostic markup.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task VerifyAsync(string source)
    {
        var test = new AnalyzeDetailedErrors.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = source + CircuitOptionsStub
        };

        await test.RunAsync(CancellationToken.None);
    }
}
