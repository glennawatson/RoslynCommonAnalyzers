// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Immutable;
using System.Runtime.CompilerServices;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Testing;

using RoslynCommon.Analyzers.Tests;

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

    /// <summary>Verifies syntax and semantic near misses preserve their existing Append calls.</summary>
    /// <param name="expression">The candidate invocation.</param>
    /// <param name="cancellationToken">The test cancellation token.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("builder.Append(text, 0, 1)")]
    [Arguments("builder.Append(GetText())")]
    [Arguments("builder.Append(value.ToString<int>())")]
    [Arguments("builder.Append(value?.ToString())")]
    [Arguments("builder.Append(value.ToString())")]
    [Arguments("builder.Append(optional.ToString())")]
    [Arguments("builder.Append(generic.ToString())")]
    [Arguments("builder.Append(dynamicValue.ToString())")]
    [Arguments("builder.Append(Missing.ToString())")]
    [Arguments("builder.Append(C.ToString())")]
    [Arguments("builder.Append(C.Format())")]
    [Arguments("builder.Append(C.Substring(1))")]
    [Arguments("builder.Append(GetText.ToString())")]
    [Arguments("builder.Append(((int item) => item).ToString())")]
    [Arguments("builder.Append(value.Substring(1))")]
    [Arguments("builder.Append(value.Substring())")]
    [Arguments("builder.Append(value.Substring(1, 2, 3))")]
    [Arguments("builder.Append(value.Format())")]
    [Arguments("builder.Append(value.Render())")]
    [Arguments("builder.Append(value->ToString())")]
    [Arguments("builder.Append(value->text.Substring(1))")]
    [Arguments("builder->Append(text.ToString())")]
    [Arguments("builder.Append(text[0].ToString(), 1)")]
    [Arguments("builder.Append(texts[0].Substring(1))")]
    [Arguments("value.Append(text.ToString())")]
    [Arguments("value.Append(string.Format(\"{0}\", text))")]
    [Arguments("C.Append(text.ToString())")]
    public async Task UnrewritableAppendCallsAreCleanAsync(string expression, CancellationToken cancellationToken)
    {
        var source = $$"""
                       class C
                       {
                           public static new string ToString() => "";
                           public static string Format() => "";
                           public static string Substring(int start) => "";
                           public static void Append(string text) { }
                           static string GetText() => "";
                           void M<T>(System.Text.StringBuilder builder, Other value, int? optional, T generic, dynamic dynamicValue, string text, string[] texts)
                           {
                               {{expression}};
                           }
                       }
                       class Other
                       {
                           public void Append(string text) { }
                           public string Format() => "";
                           public string Render() => "";
                           public string Substring(params int[] indices) => "";
                       }
                       """;
        var diagnostics = await AnalyzeAsync(source, RuntimeMetadataReferences.Platform, cancellationToken);
        await Assert.That(diagnostics).IsEmpty();
    }

    /// <summary>Verifies member receiver chains remain eligible for substring rewrites.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task SubstringOnThisMemberIsReportedAndFixedAsync() =>
        VerifyNet90Async(
            """
            class C
            {
                string text = "abc";
                void M(System.Text.StringBuilder builder) => builder.{|PSH1203:Append|}(this.text.Substring(1));
            }
            """,
            """
            class C
            {
                string text = "abc";
                void M(System.Text.StringBuilder builder) => builder.Append(this.text, 1, this.text.Length - 1);
            }
            """);

    /// <summary>Verifies unavailable replacement overloads disable the corresponding suggestion.</summary>
    /// <param name="members">The replacement members exposed by the builder.</param>
    /// <param name="argument">The inner call passed to Append.</param>
    /// <param name="cancellationToken">The test cancellation token.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("", "string.Format(\"{0}\", value)")]
    [Arguments("public static void AppendFormat(string text) { }", "string.Format(\"{0}\", value)")]
    [Arguments("public object AppendFormat;", "string.Format(\"{0}\", value)")]
    [Arguments("", "text.Substring(1)")]
    [Arguments("public void Append(string text, int start, long count) { }", "text.Substring(1)")]
    [Arguments("public void Append(string text, long start, int count) { }", "text.Substring(1)")]
    [Arguments("public void Append(object text, int start, int count) { }", "text.Substring(1)")]
    [Arguments("public static void Append(int value) { }", "value.ToString()")]
    [Arguments("", "value.ToString()")]
    public async Task MissingReplacementOverloadIsCleanAsync(string members, string argument, CancellationToken cancellationToken)
    {
        var source = $$"""
                       namespace System.Text
                       {
                           public class StringBuilder
                           {
                               public void Append(string value) { }
                               {{members}}
                           }
                       }
                       class C
                       {
                           void M(System.Text.StringBuilder builder, string text, int value) => builder.Append({{argument}});
                       }
                       """;
        var diagnostics = await AnalyzeAsync(source, RuntimeMetadataReferences.Platform, cancellationToken);
        await Assert.That(diagnostics).IsEmpty();
    }

    /// <summary>Verifies a missing builder type is cached without producing a diagnostic.</summary>
    /// <param name="cancellationToken">The test cancellation token.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task MissingBuilderTypeIsCleanAsync(CancellationToken cancellationToken)
    {
        const string Source = "class C { void M(object b, int n) { b.Append(n.ToString()); b.Append(n.ToString()); } }";
        var diagnostics = await AnalyzeAsync(Source, [], cancellationToken);
        await Assert.That(diagnostics).IsEmpty();
    }

    /// <summary>Verifies a same-named field is not an instance Append overload.</summary>
    /// <param name="cancellationToken">The test cancellation token.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task AppendDelegateFieldIsCleanAsync(CancellationToken cancellationToken)
    {
        const string Source = """
                              namespace System.Text
                              {
                                  public class StringBuilder { public System.Action<string> Append; }
                              }
                              class C { void M(System.Text.StringBuilder builder, int value) => builder.Append(value.ToString()); }
                              """;
        var diagnostics = await AnalyzeAsync(Source, RuntimeMetadataReferences.Platform, cancellationToken);
        await Assert.That(diagnostics).IsEmpty();
    }

    /// <summary>Verifies Append overloads with a different parameter contract are left alone.</summary>
    /// <param name="parameters">The custom Append parameter list.</param>
    /// <param name="cancellationToken">The test cancellation token.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("string text, int count = 1")]
    [Arguments("object text")]
    public async Task NonStringAppendOverloadsAreCleanAsync(string parameters, CancellationToken cancellationToken)
    {
        var source = $$"""
                       namespace System.Text
                       {
                           class StringBuilder { public void Append({{parameters}}) { } }
                       }
                       class C { void M(System.Text.StringBuilder builder, string value) => builder.Append(value.ToString()); }
                       """;
        var diagnostics = await AnalyzeAsync(source, RuntimeMetadataReferences.Platform, cancellationToken);
        await Assert.That(diagnostics).IsEmpty();
    }

    /// <summary>Verifies a parameterless call that binds to an optional ToString parameter is not rewritten.</summary>
    /// <param name="cancellationToken">The test cancellation token.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task OptionalToStringParameterIsCleanAsync(CancellationToken cancellationToken)
    {
        const string Source = """
                              class Value
                              {
                                  public string ToString(int format = 0) => "";
                              }
                              class C { void M(System.Text.StringBuilder builder, Value value) => builder.Append(value.ToString()); }
                              """;
        var diagnostics = await AnalyzeAsync(Source, RuntimeMetadataReferences.Platform, cancellationToken);
        await Assert.That(diagnostics).IsEmpty();
    }

    /// <summary>Runs the analyzer against incomplete or alternate framework sources.</summary>
    /// <param name="source">The compilation source.</param>
    /// <param name="references">Cached references, or none for a missing framework.</param>
    /// <param name="cancellationToken">The test cancellation token.</param>
    /// <returns>The analyzer diagnostics.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Task<ImmutableArray<Diagnostic>> AnalyzeAsync(string source, ImmutableArray<MetadataReference> references, CancellationToken cancellationToken) =>
        CSharpCompilation.Create("BuilderCalls", [CSharpSyntaxTree.ParseText(source, cancellationToken: cancellationToken)], references, new(OutputKind.DynamicallyLinkedLibrary))
            .WithAnalyzers([new Psh1203StringBuilderInnerAllocationAnalyzer()])
            .GetAnalyzerDiagnosticsAsync(cancellationToken);

    /// <summary>Runs a code-fix verification against the .NET 9 reference assemblies.</summary>
    /// <param name="source">The source with diagnostic markup.</param>
    /// <param name="fixedSource">The expected fixed source.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task VerifyNet90Async(string source, string fixedSource)
    {
        var test = new VerifyInnerAllocation.Test { ReferenceAssemblies = ReferenceAssemblies.Net.Net90, TestCode = source, FixedCode = fixedSource };

        await test.RunAsync(CancellationToken.None);
    }
}
