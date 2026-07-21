// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyInstanceExtension = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.Sst2256UseInstanceExtensionInvocationAnalyzer,
    StyleSharp.Analyzers.Sst2256UseInstanceExtensionInvocationCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for <see cref="Sst2256UseInstanceExtensionInvocationAnalyzer"/> and its code fix (SST2256).</summary>
public class UseInstanceExtensionInvocationAnalyzerUnitTest
{
    /// <summary>Verifies a static-form extension call is reported and rewritten to instance form.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task StaticFormIsFlaggedAndFixedAsync()
    {
        const string Source = """
                              internal static class Extensions
                              {
                                  public static string Wrap(this string value, string prefix) => prefix + value;
                              }

                              internal class C
                              {
                                  public string M(string s) => Extensions.{|SST2256:Wrap|}(s, ">");
                              }
                              """;
        const string FixedSource = """
                                   internal static class Extensions
                                   {
                                       public static string Wrap(this string value, string prefix) => prefix + value;
                                   }

                                   internal class C
                                   {
                                       public string M(string s) => s.Wrap(">");
                                   }
                                   """;
        await VerifyInstanceExtension.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a literal receiver stays valid in instance form.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task LiteralReceiverIsFlaggedAndFixedAsync()
    {
        const string Source = """
                              internal static class Extensions
                              {
                                  public static int Twice(this int value) => value * 2;
                              }

                              internal class C
                              {
                                  public int M() => Extensions.{|SST2256:Twice|}(3);
                              }
                              """;
        const string FixedSource = """
                                   internal static class Extensions
                                   {
                                       public static int Twice(this int value) => value * 2;
                                   }

                                   internal class C
                                   {
                                       public int M() => 3.Twice();
                                   }
                                   """;
        await VerifyInstanceExtension.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a non-primary receiver is parenthesized in instance form.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NonPrimaryReceiverIsParenthesizedAsync()
    {
        const string Source = """
                              internal static class Extensions
                              {
                                  public static string Wrap(this string value, string prefix) => prefix + value;
                              }

                              internal class C
                              {
                                  public string M(string a, string b) => Extensions.{|SST2256:Wrap|}(a + b, ">");
                              }
                              """;
        const string FixedSource = """
                                   internal static class Extensions
                                   {
                                       public static string Wrap(this string value, string prefix) => prefix + value;
                                   }

                                   internal class C
                                   {
                                       public string M(string a, string b) => (a + b).Wrap(">");
                                   }
                                   """;
        await VerifyInstanceExtension.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a call already in instance form is left alone.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task InstanceFormIsCleanAsync()
        => await VerifyInstanceExtension.VerifyAnalyzerAsync(
            """
            internal static class Extensions
            {
                public static string Wrap(this string value, string prefix) => prefix + value;
            }

            internal class C
            {
                public string M(string s) => s.Wrap(">");
            }
            """);

    /// <summary>Verifies a plain static method that is not an extension is left alone.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NonExtensionStaticCallIsCleanAsync()
        => await VerifyInstanceExtension.VerifyAnalyzerAsync(
            """
            internal class C
            {
                public int M() => int.Parse("3");
            }
            """);
}
