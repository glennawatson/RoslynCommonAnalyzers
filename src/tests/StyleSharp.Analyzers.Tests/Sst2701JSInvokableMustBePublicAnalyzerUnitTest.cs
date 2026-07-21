// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyJSInvokable = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<StyleSharp.Analyzers.Sst2701JSInvokableMustBePublicAnalyzer>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>
/// Unit tests for <see cref="Sst2701JSInvokableMustBePublicAnalyzer"/> (SST2701), which reports a method
/// annotated <c>[JSInvokable]</c> that is not public and so is silently uncallable from JavaScript at runtime.
/// </summary>
public class Sst2701JSInvokableMustBePublicAnalyzerUnitTest
{
    /// <summary>
    /// An in-source stub of the JavaScript-interop invokable attribute, added as a second document so the marker
    /// resolves without a package restore. Omitting it is what the "not referenced" gate test relies on.
    /// </summary>
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

    /// <summary>Verifies a private invokable method is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task PrivateInvokableMethodIsReportedAsync()
    {
        const string Source = """
                              using Microsoft.JSInterop;

                              public class Widget
                              {
                                  [JSInvokable]
                                  private void {|SST2701:Ping|}() { }
                              }
                              """;
        await VerifyAsync(Source);
    }

    /// <summary>Verifies an internal invokable method is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task InternalInvokableMethodIsReportedAsync()
    {
        const string Source = """
                              using Microsoft.JSInterop;

                              public class Widget
                              {
                                  [JSInvokable]
                                  internal void {|SST2701:Ping|}() { }
                              }
                              """;
        await VerifyAsync(Source);
    }

    /// <summary>Verifies a protected invokable method is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ProtectedInvokableMethodIsReportedAsync()
    {
        const string Source = """
                              using Microsoft.JSInterop;

                              public class Widget
                              {
                                  [JSInvokable]
                                  protected void {|SST2701:Ping|}() { }
                              }
                              """;
        await VerifyAsync(Source);
    }

    /// <summary>Verifies a method with no accessibility modifier — implicitly private — is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ImplicitlyPrivateInvokableMethodIsReportedAsync()
    {
        const string Source = """
                              using Microsoft.JSInterop;

                              public class Widget
                              {
                                  [JSInvokable]
                                  void {|SST2701:Ping|}() { }
                              }
                              """;
        await VerifyAsync(Source);
    }

    /// <summary>Verifies a private static invokable method is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task PrivateStaticInvokableMethodIsReportedAsync()
    {
        const string Source = """
                              using Microsoft.JSInterop;

                              public class Widget
                              {
                                  [JSInvokable("ping")]
                                  private static void {|SST2701:Ping|}() { }
                              }
                              """;
        await VerifyAsync(Source);
    }

    /// <summary>Verifies a public invokable method is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task PublicInvokableMethodIsSilentAsync()
    {
        const string Source = """
                              using Microsoft.JSInterop;

                              public class Widget
                              {
                                  [JSInvokable]
                                  public void Ping() { }
                              }
                              """;
        await VerifyAsync(Source);
    }

    /// <summary>Verifies a non-public method without the attribute is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NonPublicMethodWithoutAttributeIsSilentAsync()
    {
        const string Source = """
                              using Microsoft.JSInterop;

                              public class Widget
                              {
                                  private void Ping() { }
                              }
                              """;
        await VerifyAsync(Source);
    }

    /// <summary>Verifies the rule stays silent when no JavaScript-interop assembly is referenced.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    /// <remarks>
    /// The interop stub is deliberately not added, so the marker does not resolve and the analyzer registers
    /// nothing. The attribute here binds to a look-alike in a non-interop namespace, proving the gate rejects the
    /// shape on the marker type, not on the written name.
    /// </remarks>
    [Test]
    public async Task SilentWhenJSInteropNotReferencedAsync()
    {
        const string Source = """
                              using System;

                              namespace Look
                              {
                                  [AttributeUsage(AttributeTargets.Method)]
                                  public sealed class JSInvokableAttribute : Attribute { }

                                  public class Widget
                                  {
                                      [JSInvokable]
                                      private void Ping() { }
                                  }
                              }
                              """;

        var test = new VerifyJSInvokable.Test { TestCode = Source };
        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Runs the analyzer against the source plus the interop marker stub.</summary>
    /// <param name="source">The source with diagnostic markup.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task VerifyAsync(string source)
    {
        var test = new VerifyJSInvokable.Test { TestCode = source };
        test.TestState.Sources.Add(("JSInteropStub.cs", JSInteropStub));
        await test.RunAsync(CancellationToken.None);
    }
}
