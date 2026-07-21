// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyReference = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<StyleSharp.Analyzers.Sst2713UnstoredDotNetObjectReferenceAnalyzer>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>
/// Unit tests for <see cref="Sst2713UnstoredDotNetObjectReferenceAnalyzer"/> (SST2713), which reports a
/// <c>DotNetObjectReference.Create(...)</c> whose result is not stored in a field or property and so can never
/// be disposed.
/// </summary>
public class Sst2713UnstoredDotNetObjectReferenceAnalyzerUnitTest
{
    /// <summary>
    /// An in-source stub of the interop callback reference and its factory, added as a second document so the
    /// marker resolves without a package restore. Omitting it is what the "not referenced" gate test relies on.
    /// </summary>
    private const string InteropStub = """
                                       namespace Microsoft.JSInterop
                                       {
                                           public sealed class DotNetObjectReference<TValue> : System.IDisposable
                                               where TValue : class
                                           {
                                               internal DotNetObjectReference() { }

                                               public void Dispose() { }
                                           }

                                           public static class DotNetObjectReference
                                           {
                                               public static DotNetObjectReference<TValue> Create<TValue>(TValue value)
                                                   where TValue : class => new DotNetObjectReference<TValue>();
                                           }
                                       }
                                       """;

    /// <summary>Verifies a reference passed inline as an interop argument is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ReferencePassedAsArgumentIsReportedAsync()
    {
        const string Source = """
                              using Microsoft.JSInterop;

                              public class Widget
                              {
                                  public void Register()
                                  {
                                      Use({|SST2713:DotNetObjectReference.Create(this)|});
                                  }

                                  private static void Use(object reference) { }
                              }
                              """;
        await VerifyAsync(Source);
    }

    /// <summary>Verifies a reference created as a bare statement is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ReferenceAsBareStatementIsReportedAsync()
    {
        const string Source = """
                              using Microsoft.JSInterop;

                              public class Widget
                              {
                                  public void Register()
                                  {
                                      {|SST2713:DotNetObjectReference.Create(this)|};
                                  }
                              }
                              """;
        await VerifyAsync(Source);
    }

    /// <summary>Verifies a reference assigned to a discard is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ReferenceAssignedToDiscardIsReportedAsync()
    {
        const string Source = """
                              using Microsoft.JSInterop;

                              public class Widget
                              {
                                  public void Register()
                                  {
                                      _ = {|SST2713:DotNetObjectReference.Create(this)|};
                                  }
                              }
                              """;
        await VerifyAsync(Source);
    }

    /// <summary>Verifies a reference stored in a field is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ReferenceStoredInFieldIsSilentAsync()
    {
        const string Source = """
                              using Microsoft.JSInterop;

                              public class Widget
                              {
                                  private DotNetObjectReference<Widget> _reference;

                                  public void Register()
                                  {
                                      _reference = DotNetObjectReference.Create(this);
                                  }
                              }
                              """;
        await VerifyAsync(Source);
    }

    /// <summary>Verifies a reference held by a using declaration is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ReferenceInUsingDeclarationIsSilentAsync()
    {
        const string Source = """
                              using Microsoft.JSInterop;

                              public class Widget
                              {
                                  public void Register()
                                  {
                                      using var reference = DotNetObjectReference.Create(this);
                                  }
                              }
                              """;
        await VerifyAsync(Source);
    }

    /// <summary>Verifies a reference returned to the caller is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ReferenceReturnedIsSilentAsync()
    {
        const string Source = """
                              using Microsoft.JSInterop;

                              public class Widget
                              {
                                  public DotNetObjectReference<Widget> Wrap()
                                  {
                                      return DotNetObjectReference.Create(this);
                                  }
                              }
                              """;
        await VerifyAsync(Source);
    }

    /// <summary>Verifies the rule stays silent when no JavaScript-interop assembly is referenced.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    /// <remarks>
    /// The interop stub is deliberately not added, so the marker does not resolve and the analyzer registers
    /// nothing. The factory here is a look-alike in a non-interop namespace, proving the gate rejects the shape
    /// on the marker type, not on the written name.
    /// </remarks>
    [Test]
    public async Task SilentWhenInteropNotReferencedAsync()
    {
        const string Source = """
                              namespace Look
                              {
                                  public static class DotNetObjectReference
                                  {
                                      public static object Create(object value) => value;
                                  }

                                  public class Widget
                                  {
                                      public void Register() => Use(DotNetObjectReference.Create(this));

                                      private static void Use(object reference) { }
                                  }
                              }
                              """;

        var test = new VerifyReference.Test { TestCode = Source };
        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Runs the analyzer against the source plus the interop marker stub.</summary>
    /// <param name="source">The source with diagnostic markup.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task VerifyAsync(string source)
    {
        var test = new VerifyReference.Test { TestCode = source };
        test.TestState.Sources.Add(("InteropStub.cs", InteropStub));
        await test.RunAsync(CancellationToken.None);
    }
}
