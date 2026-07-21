// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Testing;

using AnalyzeDisclosure = SecuritySharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    SecuritySharp.Analyzers.Ses1707WebAssemblySecretDisclosureAnalyzer>;

namespace SecuritySharp.Analyzers.Tests;

/// <summary>Unit tests for SES1707 (do not hard-code secrets in code that runs in the browser as WebAssembly).</summary>
public class WebAssemblySecretDisclosureAnalyzerUnitTest
{
    /// <summary>A recognised AWS access-key-id secret shape reused across the reachability tests.</summary>
    private const string Secret = "AKIAIOSFODNN7EXAMPLE";

    /// <summary>Inline stubs of the render-mode surface: the marker gate plus WebAssembly, Auto, and Server modes.</summary>
    private const string RenderModeStub = """

                                          namespace Microsoft.AspNetCore.Components
                                          {
                                              public interface IComponentRenderMode { }

                                              [System.AttributeUsage(System.AttributeTargets.Class)]
                                              public abstract class RenderModeAttribute : System.Attribute
                                              {
                                                  public abstract IComponentRenderMode Mode { get; }
                                              }
                                          }

                                          namespace Microsoft.AspNetCore.Components.Web
                                          {
                                              public sealed class InteractiveWebAssemblyRenderMode : Microsoft.AspNetCore.Components.IComponentRenderMode { }

                                              public sealed class InteractiveAutoRenderMode : Microsoft.AspNetCore.Components.IComponentRenderMode { }

                                              public sealed class InteractiveServerRenderMode : Microsoft.AspNetCore.Components.IComponentRenderMode { }
                                          }

                                          public sealed class WasmModeAttribute : Microsoft.AspNetCore.Components.RenderModeAttribute
                                          {
                                              public override Microsoft.AspNetCore.Components.IComponentRenderMode Mode
                                                  => new Microsoft.AspNetCore.Components.Web.InteractiveWebAssemblyRenderMode();
                                          }

                                          public sealed class AutoModeAttribute : Microsoft.AspNetCore.Components.RenderModeAttribute
                                          {
                                              public override Microsoft.AspNetCore.Components.IComponentRenderMode Mode
                                                  => new Microsoft.AspNetCore.Components.Web.InteractiveAutoRenderMode();
                                          }

                                          public sealed class ServerModeAttribute : Microsoft.AspNetCore.Components.RenderModeAttribute
                                          {
                                              public override Microsoft.AspNetCore.Components.IComponentRenderMode Mode
                                                  => new Microsoft.AspNetCore.Components.Web.InteractiveServerRenderMode();
                                          }
                                          """;

    /// <summary>Inline stub of the standalone WebAssembly host builder, whose whole assembly downloads to the browser.</summary>
    private const string HostBuilderStub = """

                                           namespace Microsoft.AspNetCore.Components.WebAssembly.Hosting
                                           {
                                               public sealed class WebAssemblyHostBuilder { }
                                           }
                                           """;

    /// <summary>Verifies a secret in a component with a WebAssembly render-mode attribute is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task WebAssemblyComponentSecretReportedAsync()
        => await VerifyWithRenderModesAsync(
            $$"""
              [WasmMode]
              public class Counter
              {
                  private const string Key = {|SES1707:"{{Secret}}"|};
              }
              """);

    /// <summary>Verifies a secret in a component with an Auto render-mode attribute is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task AutoComponentSecretReportedAsync()
        => await VerifyWithRenderModesAsync(
            $$"""
              [AutoMode]
              public class Counter
              {
                  private const string Key = {|SES1707:"{{Secret}}"|};
              }
              """);

    /// <summary>Verifies a secret in a server-rendered component is not reported: the text never leaves the server.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ServerComponentSecretIsCleanAsync()
        => await VerifyWithRenderModesAsync(
            $$"""
              [ServerMode]
              public class Counter
              {
                  private const string Key = "{{Secret}}";
              }
              """);

    /// <summary>Verifies a secret in a type with no render-mode attribute is not reported when the assembly is not a host.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task UnmarkedTypeSecretIsCleanAsync()
        => await VerifyWithRenderModesAsync(
            $$"""
              public class Plain
              {
                  private const string Key = "{{Secret}}";
              }
              """);

    /// <summary>Verifies any secret is reported in a standalone WebAssembly host, whose whole assembly downloads to the browser.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task WebAssemblyHostSecretReportedAsync()
        => await VerifyWithHostBuilderAsync(
            $$"""
              public class Program
              {
                  private const string Key = {|SES1707:"{{Secret}}"|};
              }
              """);

    /// <summary>Verifies an ordinary string in a WebAssembly host is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NonSecretInWebAssemblyHostIsCleanAsync()
        => await VerifyWithHostBuilderAsync(
            """
            public class Program
            {
                private const string Message = "loading the counter, please wait";
            }
            """);

    /// <summary>Verifies the rule stays silent when neither the render-mode marker nor a WebAssembly host is present.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SilentWhenNoWebAssemblySurfaceAsync()
    {
        var test = new AnalyzeDisclosure.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = $$"""
                        public class Plain
                        {
                            private const string Key = "{{Secret}}";
                        }
                        """
        };

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Runs an analyzer-only verification with the render-mode stubs (but no WebAssembly host) appended.</summary>
    /// <param name="source">The source with diagnostic markup.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task VerifyWithRenderModesAsync(string source)
    {
        var test = new AnalyzeDisclosure.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = source + RenderModeStub
        };

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Runs an analyzer-only verification with the WebAssembly host-builder stub appended.</summary>
    /// <param name="source">The source with diagnostic markup.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task VerifyWithHostBuilderAsync(string source)
    {
        var test = new AnalyzeDisclosure.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = source + HostBuilderStub
        };

        await test.RunAsync(CancellationToken.None);
    }
}
