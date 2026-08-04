// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifySelfRecursiveForwarder = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    StyleSharp.Analyzers.Sst2497SelfRecursiveForwarderAnalyzer>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for <see cref="Sst2497SelfRecursiveForwarderAnalyzer"/> (SST2497).</summary>
public class SelfRecursiveForwarderAnalyzerUnitTest
{
    /// <summary>Verifies an overload that forwards to itself instead of its sibling is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task OverloadForwardingToItselfIsReportedAsync()
    {
        const string Source = """
                              using System;

                              public sealed class Cache
                              {
                                  public string Process(string body, string url, DateTimeOffset? expiry)
                                      => body;

                                  public string Process(string body, Uri url, DateTimeOffset? expiry)
                                      => {|SST2497:Process(body, url, expiry)|};
                              }
                              """;
        await VerifySelfRecursiveForwarder.VerifyAnalyzerAsync(Source);
    }

    /// <summary>Verifies an override that forwards to itself rather than to the base implementation is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task OverrideForwardingToItselfIsReportedAsync()
    {
        const string Source = """
                              public abstract class Base
                              {
                                  public virtual int Scale(int value) => value;
                              }

                              public sealed class Derived : Base
                              {
                                  public override int Scale(int value) => {|SST2497:Scale(value)|};
                              }
                              """;
        await VerifySelfRecursiveForwarder.VerifyAnalyzerAsync(Source);
    }

    /// <summary>Verifies a this-qualified self call is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ThisQualifiedSelfCallIsReportedAsync()
    {
        const string Source = """
                              public sealed class C
                              {
                                  public int Double(int value) => {|SST2497:this.Double(value)|};
                              }
                              """;
        await VerifySelfRecursiveForwarder.VerifyAnalyzerAsync(Source);
    }

    /// <summary>Verifies a parameterless member that calls itself is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ParameterlessSelfCallIsReportedAsync()
    {
        const string Source = """
                              public sealed class C
                              {
                                  public int Next() => {|SST2497:Next()|};
                              }
                              """;
        await VerifySelfRecursiveForwarder.VerifyAnalyzerAsync(Source);
    }

    /// <summary>Verifies a local function that forwards to itself is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task LocalFunctionForwardingToItselfIsReportedAsync()
    {
        const string Source = """
                              public sealed class C
                              {
                                  public int M(int seed)
                                  {
                                      return Step(seed);

                                      int Step(int value) => {|SST2497:Step(value)|};
                                  }
                              }
                              """;
        await VerifySelfRecursiveForwarder.VerifyAnalyzerAsync(Source);
    }

    /// <summary>Verifies a call that reaches a different member, or that can terminate, is clean.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task CallsReachingAnotherMemberAreCleanAsync()
    {
        const string Source = """
                              using System;

                              public abstract class Base
                              {
                                  public virtual int Scale(int value) => value;
                              }

                              public sealed class C : Base
                              {
                                  public override int Scale(int value) => base.Scale(value);

                                  public string Render(string body, Uri url) => Render(body, url.ToString());

                                  public string Render(string body, string url) => body + url;

                                  public int Countdown(int value) => value <= 0 ? 0 : Countdown(value - 1);

                                  public int Forwarded(int value) => Helper(value);

                                  private int Helper(int value) => value;
                              }
                              """;
        await VerifySelfRecursiveForwarder.VerifyAnalyzerAsync(Source);
    }

    /// <summary>
    /// Verifies a self-call whose arguments are not the parameters in their own positions is left alone.
    /// Reordered and named arguments recurse just as endlessly, but the positional match is what proves
    /// which member the call reaches, so these sit outside what this rule claims.
    /// </summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SelfCallsOutsideThePositionalProofAreNotReportedAsync()
    {
        const string Source = """
                              public sealed class C
                              {
                                  public int Swapped(int first, int second) => Swapped(second, first);

                                  public int Named(int first, int second) => Named(second: second, first: first);
                              }
                              """;
        await VerifySelfRecursiveForwarder.VerifyAnalyzerAsync(Source);
    }
}
