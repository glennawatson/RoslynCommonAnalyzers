// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyInjected = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<StyleSharp.Analyzers.Sst2712SetterlessInjectedPropertyAnalyzer>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>
/// Unit tests for <see cref="Sst2712SetterlessInjectedPropertyAnalyzer"/> (SST2712), which reports an
/// <c>[Inject]</c> or <c>[CascadingParameter]</c> property that has no setter and so is never assigned.
/// </summary>
public class Sst2712SetterlessInjectedPropertyAnalyzerUnitTest
{
    /// <summary>
    /// An in-source stub of the injection and cascading marker attributes, added as a second document so the
    /// markers resolve without a package restore. Omitting it is what the "not referenced" gate test relies on.
    /// </summary>
    private const string ComponentsStub = """
                                          namespace Microsoft.AspNetCore.Components
                                          {
                                              [System.AttributeUsage(System.AttributeTargets.Property)]
                                              public sealed class InjectAttribute : System.Attribute { }

                                              [System.AttributeUsage(System.AttributeTargets.Property)]
                                              public sealed class CascadingParameterAttribute : System.Attribute { }
                                          }
                                          """;

    /// <summary>Verifies a get-only injected property is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task GetOnlyInjectedPropertyIsReportedAsync()
    {
        const string Source = """
                              using Microsoft.AspNetCore.Components;

                              public class Widget
                              {
                                  [Inject]
                                  public string {|SST2712:Service|} { get; }
                              }
                              """;
        await VerifyAsync(Source);
    }

    /// <summary>Verifies a get-only cascading property is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task GetOnlyCascadingPropertyIsReportedAsync()
    {
        const string Source = """
                              using Microsoft.AspNetCore.Components;

                              public class Widget
                              {
                                  [CascadingParameter]
                                  public string {|SST2712:Theme|} { get; }
                              }
                              """;
        await VerifyAsync(Source);
    }

    /// <summary>Verifies an expression-bodied injected property is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ExpressionBodiedInjectedPropertyIsReportedAsync()
    {
        const string Source = """
                              using Microsoft.AspNetCore.Components;

                              public class Widget
                              {
                                  [Inject]
                                  public string {|SST2712:Service|} => "unset";
                              }
                              """;
        await VerifyAsync(Source);
    }

    /// <summary>Verifies an injected property with a public setter is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task InjectedPropertyWithSetterIsSilentAsync()
    {
        const string Source = """
                              using Microsoft.AspNetCore.Components;

                              public class Widget
                              {
                                  [Inject]
                                  public string Service { get; set; }
                              }
                              """;
        await VerifyAsync(Source);
    }

    /// <summary>Verifies an injected property with a private setter is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task InjectedPropertyWithPrivateSetterIsSilentAsync()
    {
        const string Source = """
                              using Microsoft.AspNetCore.Components;

                              public class Widget
                              {
                                  [Inject]
                                  public string Service { get; private set; }
                              }
                              """;
        await VerifyAsync(Source);
    }

    /// <summary>Verifies an injected property with an init accessor is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task InjectedPropertyWithInitAccessorIsSilentAsync()
    {
        const string Source = """
                              using Microsoft.AspNetCore.Components;

                              public class Widget
                              {
                                  [Inject]
                                  public string Service { get; init; }
                              }
                              """;
        await VerifyAsync(Source);
    }

    /// <summary>Verifies a get-only property without either marker is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task GetOnlyPropertyWithoutMarkerIsSilentAsync()
    {
        const string Source = """
                              using Microsoft.AspNetCore.Components;

                              public class Widget
                              {
                                  public string Service { get; }
                              }
                              """;
        await VerifyAsync(Source);
    }

    /// <summary>Verifies the rule stays silent when no component assembly is referenced.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    /// <remarks>
    /// The marker stub is deliberately not added, so neither attribute resolves and the analyzer registers
    /// nothing. The attribute here binds to a look-alike in a non-component namespace, proving the gate rejects
    /// the shape on the marker type, not on the written name.
    /// </remarks>
    [Test]
    public async Task SilentWhenComponentsNotReferencedAsync()
    {
        const string Source = """
                              namespace Look
                              {
                                  [System.AttributeUsage(System.AttributeTargets.Property)]
                                  public sealed class InjectAttribute : System.Attribute { }

                                  public class Widget
                                  {
                                      [Inject]
                                      public string Service { get; }
                                  }
                              }
                              """;

        var test = new VerifyInjected.Test { TestCode = Source };
        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Runs the analyzer against the source plus the marker stub.</summary>
    /// <param name="source">The source with diagnostic markup.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task VerifyAsync(string source)
    {
        var test = new VerifyInjected.Test { TestCode = source };
        test.TestState.Sources.Add(("ComponentsStub.cs", ComponentsStub));
        await test.RunAsync(CancellationToken.None);
    }
}
