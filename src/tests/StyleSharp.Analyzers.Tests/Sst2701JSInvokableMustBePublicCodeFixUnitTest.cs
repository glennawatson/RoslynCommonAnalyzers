// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyJSInvokable = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.Sst2701JSInvokableMustBePublicAnalyzer,
    StyleSharp.Analyzers.Sst2701JSInvokableMustBePublicCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Tests for <see cref="Sst2701JSInvokableMustBePublicCodeFixProvider"/> (SST2701 make the method public).</summary>
public class Sst2701JSInvokableMustBePublicCodeFixUnitTest
{
    /// <summary>The interop marker stub added to both the test and fixed documents so the marker resolves.</summary>
    private const string JSInteropStub = """
                                         namespace Microsoft.JSInterop
                                         {
                                             [System.AttributeUsage(System.AttributeTargets.Method)]
                                             public sealed class JSInvokableAttribute : System.Attribute
                                             {
                                                 public JSInvokableAttribute() { }
                                                 public JSInvokableAttribute(string identifier) { }
                                             }
                                         }
                                         """;

    /// <summary>Verifies a private invokable method is made public.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task MakesPrivateMethodPublicAsync()
    {
        const string Source = """
                              using Microsoft.JSInterop;

                              public class Widget
                              {
                                  [JSInvokable]
                                  private void {|SST2701:Ping|}() { }
                              }
                              """;
        const string Fixed = """
                             using Microsoft.JSInterop;

                             public class Widget
                             {
                                 [JSInvokable]
                                 public void Ping() { }
                             }
                             """;
        await VerifyFixAsync(Source, Fixed);
    }

    /// <summary>Verifies a method with no accessibility modifier gains the public modifier.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task MakesImplicitlyPrivateMethodPublicAsync()
    {
        const string Source = """
                              using Microsoft.JSInterop;

                              public class Widget
                              {
                                  [JSInvokable]
                                  void {|SST2701:Ping|}() { }
                              }
                              """;
        const string Fixed = """
                             using Microsoft.JSInterop;

                             public class Widget
                             {
                                 [JSInvokable]
                                 public void Ping() { }
                             }
                             """;
        await VerifyFixAsync(Source, Fixed);
    }

    /// <summary>Verifies Fix All makes every reported method in the document public.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task FixAllMakesEveryReportedMethodPublicAsync()
    {
        const string Source = """
                              using Microsoft.JSInterop;

                              public class Widget
                              {
                                  [JSInvokable]
                                  private void {|SST2701:Ping|}() { }

                                  [JSInvokable]
                                  internal void {|SST2701:Pong|}() { }
                              }
                              """;
        const string Fixed = """
                             using Microsoft.JSInterop;

                             public class Widget
                             {
                                 [JSInvokable]
                                 public void Ping() { }

                                 [JSInvokable]
                                 public void Pong() { }
                             }
                             """;
        await VerifyFixAsync(Source, Fixed);
    }

    /// <summary>Runs the code fix against the source plus the interop marker stub.</summary>
    /// <param name="source">The source with diagnostic markup.</param>
    /// <param name="fixedSource">The expected source after the fix.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task VerifyFixAsync(string source, string fixedSource)
    {
        var test = new VerifyJSInvokable.Test { TestCode = source, FixedCode = fixedSource };
        test.TestState.Sources.Add(("JSInteropStub.cs", JSInteropStub));
        test.FixedState.Sources.Add(("JSInteropStub.cs", JSInteropStub));
        await test.RunAsync(CancellationToken.None);
    }
}
