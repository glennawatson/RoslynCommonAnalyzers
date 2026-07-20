// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Testing;

using AnalyzeEncoding = SecuritySharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    SecuritySharp.Analyzers.Ses1604PromptTemplateContentEncodingDisabledAnalyzer>;

namespace SecuritySharp.Analyzers.Tests;

/// <summary>Unit tests for SES1604 (a prompt template must not disable the encoding of substituted input).</summary>
public class PromptTemplateContentEncodingDisabledAnalyzerUnitTest
{
    /// <summary>Inline stub of the Semantic Kernel types carrying the <c>AllowDangerouslySetContent</c> flag.</summary>
    private const string SemanticKernelStub = """

                                              namespace Microsoft.SemanticKernel
                                              {
                                                  public class PromptTemplateConfig
                                                  {
                                                      public bool AllowDangerouslySetContent { get; set; }
                                                  }

                                                  public class InputVariable
                                                  {
                                                      public string Name { get; set; }

                                                      public bool AllowDangerouslySetContent { get; set; }
                                                  }

                                                  public class KernelPromptTemplateFactory
                                                  {
                                                      public bool AllowDangerouslySetContent { get; set; }
                                                  }
                                              }

                                              namespace Microsoft.SemanticKernel.PromptTemplates.Handlebars
                                              {
                                                  public class HandlebarsPromptTemplateFactory
                                                  {
                                                      public bool AllowDangerouslySetContent { get; set; }
                                                  }
                                              }
                                              """;

    /// <summary>Verifies a direct <c>AllowDangerouslySetContent = true</c> on the config is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task DirectAssignmentOnConfigReportedAsync()
        => await VerifyAsync(
            """
            using Microsoft.SemanticKernel;

            public class C
            {
                public void M(PromptTemplateConfig config)
                {
                    {|SES1604:config.AllowDangerouslySetContent = true|};
                }
            }
            """);

    /// <summary>Verifies the object-initializer form on the config is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ObjectInitializerOnConfigReportedAsync()
        => await VerifyAsync(
            """
            using Microsoft.SemanticKernel;

            public class C
            {
                public PromptTemplateConfig M()
                    => new PromptTemplateConfig { {|SES1604:AllowDangerouslySetContent = true|} };
            }
            """);

    /// <summary>Verifies the object-initializer form on an input variable is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ObjectInitializerOnInputVariableReportedAsync()
        => await VerifyAsync(
            """
            using Microsoft.SemanticKernel;

            public class C
            {
                public InputVariable M()
                    => new InputVariable { Name = "input", {|SES1604:AllowDangerouslySetContent = true|} };
            }
            """);

    /// <summary>Verifies the object-initializer form on the template factory is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ObjectInitializerOnKernelFactoryReportedAsync()
        => await VerifyAsync(
            """
            using Microsoft.SemanticKernel;

            public class C
            {
                public KernelPromptTemplateFactory M()
                    => new KernelPromptTemplateFactory { {|SES1604:AllowDangerouslySetContent = true|} };
            }
            """);

    /// <summary>Verifies the flag on the Handlebars template factory is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task HandlebarsFactoryReportedAsync()
        => await VerifyAsync(
            """
            using Microsoft.SemanticKernel.PromptTemplates.Handlebars;

            public class C
            {
                public void M(HandlebarsPromptTemplateFactory factory)
                {
                    {|SES1604:factory.AllowDangerouslySetContent = true|};
                }
            }
            """);

    /// <summary>Verifies setting the flag to false is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SetToFalseIsCleanAsync()
        => await VerifyAsync(
            """
            using Microsoft.SemanticKernel;

            public class C
            {
                public void M(PromptTemplateConfig config)
                {
                    config.AllowDangerouslySetContent = false;
                }
            }
            """);

    /// <summary>Verifies a non-literal right-hand side is not reported (only the literal <c>true</c> is).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NonLiteralValueIsCleanAsync()
        => await VerifyAsync(
            """
            using Microsoft.SemanticKernel;

            public class C
            {
                public void M(PromptTemplateConfig config, bool trusted)
                {
                    config.AllowDangerouslySetContent = trusted;
                }
            }
            """);

    /// <summary>Verifies a same-named property on an unrelated type is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SameNamedPropertyOnUnrelatedTypeIsCleanAsync()
        => await VerifyAsync(
            """
            public sealed class MyOptions
            {
                public bool AllowDangerouslySetContent { get; set; }
            }

            public class C
            {
                public void M(MyOptions options)
                {
                    options.AllowDangerouslySetContent = true;
                }
            }
            """);

    /// <summary>Verifies the rule stays silent when Semantic Kernel is absent from the compilation.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SilentWhenSemanticKernelUnavailableAsync()
    {
        const string Source = """
                              public sealed class PromptTemplateConfig
                              {
                                  public bool AllowDangerouslySetContent { get; set; }
                              }

                              public class C
                              {
                                  public void M(PromptTemplateConfig config)
                                  {
                                      config.AllowDangerouslySetContent = true;
                                  }
                              }
                              """;

        var test = new AnalyzeEncoding.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = Source
        };

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Runs an analyzer-only verification with the inline Semantic Kernel stub appended.</summary>
    /// <param name="source">The source with diagnostic markup.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task VerifyAsync(string source)
    {
        var test = new AnalyzeEncoding.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = source + SemanticKernelStub
        };

        await test.RunAsync(CancellationToken.None);
    }
}
