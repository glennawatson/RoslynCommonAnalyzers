// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyInjected = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.Sst2712SetterlessInjectedPropertyAnalyzer,
    StyleSharp.Analyzers.Sst2712SetterlessInjectedPropertyCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>
/// Tests for <see cref="Sst2712SetterlessInjectedPropertyCodeFixProvider"/> (SST2712 add a private setter).
/// </summary>
public class Sst2712SetterlessInjectedPropertyCodeFixUnitTest
{
    /// <summary>The marker stub added to both the test and fixed documents so the markers resolve.</summary>
    private const string ComponentsStub = """
                                          namespace Microsoft.AspNetCore.Components
                                          {
                                              [System.AttributeUsage(System.AttributeTargets.Property)]
                                              public sealed class InjectAttribute : System.Attribute { }

                                              [System.AttributeUsage(System.AttributeTargets.Property)]
                                              public sealed class CascadingParameterAttribute : System.Attribute { }
                                          }
                                          """;

    /// <summary>Verifies a get-only injected property gains a private setter.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task AddsPrivateSetterToInjectedPropertyAsync()
    {
        const string Source = """
                              using Microsoft.AspNetCore.Components;

                              public class Widget
                              {
                                  [Inject]
                                  public string {|SST2712:Service|} { get; }
                              }
                              """;
        const string Fixed = """
                             using Microsoft.AspNetCore.Components;

                             public class Widget
                             {
                                 [Inject]
                                 public string Service { get; private set; }
                             }
                             """;
        await VerifyFixAsync(Source, Fixed);
    }

    /// <summary>Verifies a get-only cascading property gains a private setter.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task AddsPrivateSetterToCascadingPropertyAsync()
    {
        const string Source = """
                              using Microsoft.AspNetCore.Components;

                              public class Widget
                              {
                                  [CascadingParameter]
                                  public string {|SST2712:Theme|} { get; }
                              }
                              """;
        const string Fixed = """
                             using Microsoft.AspNetCore.Components;

                             public class Widget
                             {
                                 [CascadingParameter]
                                 public string Theme { get; private set; }
                             }
                             """;
        await VerifyFixAsync(Source, Fixed);
    }

    /// <summary>Verifies no fix is offered for an expression-bodied property, which has no accessor list.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NoFixForExpressionBodiedPropertyAsync()
    {
        const string Source = """
                              using Microsoft.AspNetCore.Components;

                              public class Widget
                              {
                                  [Inject]
                                  public string {|SST2712:Service|} => "unset";
                              }
                              """;
        await VerifyFixAsync(Source, Source);
    }

    /// <summary>Runs the code fix against the source plus the marker stub.</summary>
    /// <param name="source">The source with diagnostic markup.</param>
    /// <param name="fixedSource">The expected source after the fix.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task VerifyFixAsync(string source, string fixedSource)
    {
        var test = new VerifyInjected.Test { TestCode = source, FixedCode = fixedSource };
        test.TestState.Sources.Add(("ComponentsStub.cs", ComponentsStub));
        test.FixedState.Sources.Add(("ComponentsStub.cs", ComponentsStub));
        await test.RunAsync(CancellationToken.None);
    }
}
