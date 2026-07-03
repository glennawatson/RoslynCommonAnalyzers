// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Testing;

using VerifyInnerAllocation = PerformanceSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    PerformanceSharp.Analyzers.Psh1203StringBuilderInnerAllocationAnalyzer,
    PerformanceSharp.Analyzers.Psh1203StringBuilderInnerAllocationCodeFixProvider>;

namespace PerformanceSharp.Analyzers.Tests;

/// <summary>Unit tests for PSH1203 (let StringBuilder do the formatting work) and its code fix.</summary>
public class StringBuilderInnerAllocationAnalyzerUnitTest
{
    /// <summary>Verifies Append of a string.Format result is reported (PSH1203) and fixed to AppendFormat.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task AppendStringFormatReplacedWithAppendFormatAsync()
    {
        const string Source = """
                              public class C
                              {
                                  public void M(System.Text.StringBuilder builder, int value)
                                      => builder.{|PSH1203:Append|}(string.Format("{0:N2}", value));
                              }
                              """;
        const string FixedSource = """
                                   public class C
                                   {
                                       public void M(System.Text.StringBuilder builder, int value)
                                           => builder.AppendFormat("{0:N2}", value);
                                   }
                                   """;
        await VerifyNet90Async(Source, FixedSource);
    }

    /// <summary>Verifies Append of a parameterless ToString result is reported and fixed to the typed overload.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task AppendToStringReplacedWithTypedAppendAsync()
    {
        const string Source = """
                              public class C
                              {
                                  public void M(System.Text.StringBuilder builder, int value)
                                      => builder.{|PSH1203:Append|}(value.ToString());
                              }
                              """;
        const string FixedSource = """
                                   public class C
                                   {
                                       public void M(System.Text.StringBuilder builder, int value)
                                           => builder.Append(value);
                                   }
                                   """;
        await VerifyNet90Async(Source, FixedSource);
    }

    /// <summary>Verifies ToString on a string receiver is reported and the identity call is dropped.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task AppendStringToStringDropsIdentityCallAsync()
    {
        const string Source = """
                              public class C
                              {
                                  public void M(System.Text.StringBuilder builder, string text)
                                      => builder.{|PSH1203:Append|}(text.ToString());
                              }
                              """;
        const string FixedSource = """
                                   public class C
                                   {
                                       public void M(System.Text.StringBuilder builder, string text)
                                           => builder.Append(text);
                                   }
                                   """;
        await VerifyNet90Async(Source, FixedSource);
    }

    /// <summary>Verifies Append of a two-argument Substring is reported and fixed to the segment overload.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task AppendSubstringWithCountReplacedAsync()
    {
        const string Source = """
                              public class C
                              {
                                  public void M(System.Text.StringBuilder builder, string text)
                                      => builder.{|PSH1203:Append|}(text.Substring(2, 3));
                              }
                              """;
        const string FixedSource = """
                                   public class C
                                   {
                                       public void M(System.Text.StringBuilder builder, string text)
                                           => builder.Append(text, 2, 3);
                                   }
                                   """;
        await VerifyNet90Async(Source, FixedSource);
    }

    /// <summary>Verifies Append of a to-end Substring is reported and the fix computes the remaining length.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task AppendSubstringToEndReplacedAsync()
    {
        const string Source = """
                              public class C
                              {
                                  public void M(System.Text.StringBuilder builder, string text)
                                      => builder.{|PSH1203:Append|}(text.Substring(2));
                              }
                              """;
        const string FixedSource = """
                                   public class C
                                   {
                                       public void M(System.Text.StringBuilder builder, string text)
                                           => builder.Append(text, 2, text.Length - 2);
                                   }
                                   """;
        await VerifyNet90Async(Source, FixedSource);
    }

    /// <summary>Verifies a plain string variable argument is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task AppendLocalStringIsCleanAsync()
    {
        const string Source = """
                              public class C
                              {
                                  public void M(System.Text.StringBuilder builder, string text)
                                      => builder.Append(text);
                              }
                              """;
        await VerifyNet90Async(Source, Source);
    }

    /// <summary>Verifies ToString with a format argument is not reported — the format changes what is appended.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task AppendToStringWithFormatIsCleanAsync()
    {
        const string Source = """
                              public class C
                              {
                                  public void M(System.Text.StringBuilder builder, double value)
                                      => builder.Append(value.ToString("F2"));
                              }
                              """;
        await VerifyNet90Async(Source, Source);
    }

    /// <summary>Verifies AppendLine of a string.Format result is not reported — AppendLine has no format overload.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task AppendLineStringFormatIsCleanAsync()
    {
        const string Source = """
                              public class C
                              {
                                  public void M(System.Text.StringBuilder builder, int value)
                                      => builder.AppendLine(string.Format("{0}", value));
                              }
                              """;
        await VerifyNet90Async(Source, Source);
    }

    /// <summary>Verifies Substring on a method-call receiver is not reported — duplicating the call is unsafe.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SubstringOnMethodCallReceiverIsCleanAsync()
    {
        const string Source = """
                              public class C
                              {
                                  public void M(System.Text.StringBuilder builder)
                                      => builder.Append(GetText().Substring(1));

                                  public string GetText() => "abc";
                              }
                              """;
        await VerifyNet90Async(Source, Source);
    }

    /// <summary>Verifies a provider-first string.Format call is fixed to AppendFormat with the provider kept.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task AppendStringFormatWithProviderKeepsProviderAsync()
    {
        const string Source = """
                              public class C
                              {
                                  public void M(System.Text.StringBuilder builder, int value)
                                      => builder.{|PSH1203:Append|}(string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0}", value));
                              }
                              """;
        const string FixedSource = """
                                   public class C
                                   {
                                       public void M(System.Text.StringBuilder builder, int value)
                                           => builder.AppendFormat(System.Globalization.CultureInfo.InvariantCulture, "{0}", value);
                                   }
                                   """;
        await VerifyNet90Async(Source, FixedSource);
    }

    /// <summary>Verifies Fix All rewrites every shape in one pass.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task FixAllAcrossShapesAsync()
    {
        const string Source = """
                              public class C
                              {
                                  public void M(System.Text.StringBuilder builder, string text, int value)
                                  {
                                      builder.{|PSH1203:Append|}(string.Format("{0}", value));
                                      builder.{|PSH1203:Append|}(value.ToString());
                                      builder.{|PSH1203:Append|}(text.Substring(1));
                                  }
                              }
                              """;
        const string FixedSource = """
                                   public class C
                                   {
                                       public void M(System.Text.StringBuilder builder, string text, int value)
                                       {
                                           builder.AppendFormat("{0}", value);
                                           builder.Append(value);
                                           builder.Append(text, 1, text.Length - 1);
                                       }
                                   }
                                   """;
        await VerifyNet90Async(Source, FixedSource);
    }

    /// <summary>Runs a code-fix verification against the .NET 9 reference assemblies.</summary>
    /// <param name="source">The source with diagnostic markup.</param>
    /// <param name="fixedSource">The expected fixed source.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task VerifyNet90Async(string source, string fixedSource)
    {
        var test = new VerifyInnerAllocation.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = source,
            FixedCode = fixedSource
        };

        await test.RunAsync(CancellationToken.None);
    }
}
