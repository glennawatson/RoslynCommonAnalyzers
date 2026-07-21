// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Testing;

using AnalyzeSerializeAllClaims = SecuritySharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    SecuritySharp.Analyzers.Ses1709SerializeAllClaimsAnalyzer>;

namespace SecuritySharp.Analyzers.Tests;

/// <summary>Unit tests for SES1709 (do not serialize every claim into the client-readable authentication state).</summary>
public class SerializeAllClaimsAnalyzerUnitTest
{
    /// <summary>Inline stub of the WebAssembly auth-state serialization options carrying the <c>SerializeAllClaims</c> flag.</summary>
    private const string OptionsStub = """

                                       namespace Microsoft.AspNetCore.Components.WebAssembly.Server
                                       {
                                           public sealed class AuthenticationStateSerializationOptions
                                           {
                                               public bool SerializeAllClaims { get; set; }
                                           }
                                       }
                                       """;

    /// <summary>Verifies a direct <c>SerializeAllClaims = true</c> assignment is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task DirectAssignmentReportedAsync()
        => await VerifyAsync(
            """
            using Microsoft.AspNetCore.Components.WebAssembly.Server;

            public class C
            {
                public void M(AuthenticationStateSerializationOptions options)
                {
                    {|SES1709:options.SerializeAllClaims = true|};
                }
            }
            """);

    /// <summary>Verifies the object-initializer form of <c>SerializeAllClaims = true</c> is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ObjectInitializerReportedAsync()
        => await VerifyAsync(
            """
            using Microsoft.AspNetCore.Components.WebAssembly.Server;

            public class C
            {
                public AuthenticationStateSerializationOptions M()
                    => new AuthenticationStateSerializationOptions { {|SES1709:SerializeAllClaims = true|} };
            }
            """);

    /// <summary>Verifies setting <c>SerializeAllClaims</c> to false is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SetToFalseIsCleanAsync()
        => await VerifyAsync(
            """
            using Microsoft.AspNetCore.Components.WebAssembly.Server;

            public class C
            {
                public void M(AuthenticationStateSerializationOptions options)
                {
                    options.SerializeAllClaims = false;
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
                public bool SerializeAllClaims { get; set; }
            }

            public class C
            {
                public void M(MyOptions options)
                {
                    options.SerializeAllClaims = true;
                }
            }
            """);

    /// <summary>Verifies the rule stays silent when the serialization-options type is absent from the compilation.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SilentWhenOptionsUnavailableAsync()
    {
        const string Source = """
                              public sealed class AuthenticationStateSerializationOptions
                              {
                                  public bool SerializeAllClaims { get; set; }
                              }

                              public class C
                              {
                                  public void M(AuthenticationStateSerializationOptions options)
                                  {
                                      options.SerializeAllClaims = true;
                                  }
                              }
                              """;

        var test = new AnalyzeSerializeAllClaims.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = Source
        };

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Runs an analyzer-only verification with the inline serialization-options stub appended.</summary>
    /// <param name="source">The source with diagnostic markup.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task VerifyAsync(string source)
    {
        var test = new AnalyzeSerializeAllClaims.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = source + OptionsStub
        };

        await test.RunAsync(CancellationToken.None);
    }
}
