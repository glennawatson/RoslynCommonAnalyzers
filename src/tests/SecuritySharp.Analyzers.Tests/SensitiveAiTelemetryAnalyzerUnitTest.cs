// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Testing;

using AnalyzeTelemetry = SecuritySharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    SecuritySharp.Analyzers.Ses1605SensitiveAiTelemetryAnalyzer>;

namespace SecuritySharp.Analyzers.Tests;

/// <summary>Unit tests for SES1605 (AI instrumentation must not enable sensitive-data capture).</summary>
public class SensitiveAiTelemetryAnalyzerUnitTest
{
    /// <summary>
    /// The shared preamble: the usings plus an inline stub of the Microsoft.Extensions.AI OpenTelemetry
    /// instrumentation clients under test. The usings precede the stub namespace so each snippet body can
    /// reference the types unqualified and declares no usings of its own.
    /// </summary>
    private const string Preamble = """
        using System;
        using Microsoft.Extensions.AI;

        namespace Microsoft.Extensions.AI
        {
            public sealed class OpenTelemetryChatClient
            {
                public OpenTelemetryChatClient(object inner)
                {
                }

                public bool EnableSensitiveData { get; set; }
            }

            public sealed class OpenTelemetryEmbeddingGenerator<TInput, TEmbedding>
            {
                public bool EnableSensitiveData { get; set; }
            }
        }


        """;

    /// <summary>Verifies a statement assignment of <c>EnableSensitiveData = true</c> on the chat client is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task StatementAssignmentOnChatClientReportedAsync()
        => await VerifyAsync(
            """
            public class C
            {
                public void M(OpenTelemetryChatClient otel)
                {
                    {|SES1605:otel.EnableSensitiveData = true|};
                }
            }
            """);

    /// <summary>Verifies an object-initializer member <c>EnableSensitiveData = true</c> is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ObjectInitializerMemberReportedAsync()
        => await VerifyAsync(
            """
            public class C
            {
                public OpenTelemetryChatClient M(object inner)
                    => new OpenTelemetryChatClient(inner) { {|SES1605:EnableSensitiveData = true|} };
            }
            """);

    /// <summary>Verifies the configure-delegate shape <c>o =&gt; o.EnableSensitiveData = true</c> is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ConfigureDelegateReportedAsync()
        => await VerifyAsync(
            """
            public class C
            {
                public void M()
                {
                    Action<OpenTelemetryChatClient> configure = o => {|SES1605:o.EnableSensitiveData = true|};
                }
            }
            """);

    /// <summary>Verifies the generic embedding-generator variant is reported (unbound-definition match).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task GenericEmbeddingGeneratorReportedAsync()
        => await VerifyAsync(
            """
            public class C
            {
                public void M(OpenTelemetryEmbeddingGenerator<string, float[]> otel)
                {
                    {|SES1605:otel.EnableSensitiveData = true|};
                }
            }
            """);

    /// <summary>Verifies a <c>const</c> that evaluates to <c>true</c> is reported (compile-time constant).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ConstantTrueReportedAsync()
        => await VerifyAsync(
            """
            public class C
            {
                private const bool Capture = true;

                public void M(OpenTelemetryChatClient otel)
                {
                    {|SES1605:otel.EnableSensitiveData = Capture|};
                }
            }
            """);

    /// <summary>Verifies setting <c>EnableSensitiveData = false</c> is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task FalseAssignmentIsCleanAsync()
        => await VerifyAsync(
            """
            public class C
            {
                public void M(OpenTelemetryChatClient otel)
                {
                    otel.EnableSensitiveData = false;
                }
            }
            """);

    /// <summary>Verifies a runtime-computed value is not reported (only a compile-time <c>true</c> is).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task RuntimeValueIsCleanAsync()
        => await VerifyAsync(
            """
            public class C
            {
                public void M(OpenTelemetryChatClient otel, bool flag)
                {
                    otel.EnableSensitiveData = flag;
                }
            }
            """);

    /// <summary>Verifies a same-named property on an unrelated type is not reported (binding gate).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task UnrelatedTypeIsCleanAsync()
        => await VerifyAsync(
            """
            namespace Other
            {
                public sealed class Widget
                {
                    public bool EnableSensitiveData { get; set; }
                }
            }

            public class C
            {
                public void M(Other.Widget widget, OpenTelemetryChatClient otel)
                {
                    widget.EnableSensitiveData = true;
                    {|SES1605:otel.EnableSensitiveData = true|};
                }
            }
            """);

    /// <summary>Verifies the rule stays silent when no instrumentation type resolves in the compilation.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SilentWhenInstrumentationAbsentAsync()
    {
        const string Source = """
                              namespace Other
                              {
                                  public sealed class Telemetry
                                  {
                                      public bool EnableSensitiveData { get; set; }
                                  }

                                  public class C
                                  {
                                      public void M(Telemetry telemetry)
                                      {
                                          telemetry.EnableSensitiveData = true;
                                      }
                                  }
                              }
                              """;

        var test = new AnalyzeTelemetry.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = Source
        };

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Runs an analyzer-only verification against the .NET 9 reference assemblies with the instrumentation stub prepended.</summary>
    /// <param name="source">The user source with diagnostic markup (no usings; the preamble supplies them).</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task VerifyAsync(string source)
    {
        var test = new AnalyzeTelemetry.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = Preamble + source
        };

        await test.RunAsync(CancellationToken.None);
    }
}
