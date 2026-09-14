// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using RoslynCommon.Analyzers.Tests;

using AnalyzeXslt = SecuritySharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    SecuritySharp.Analyzers.Ses1309XsltScriptExecutionAnalyzer>;

namespace SecuritySharp.Analyzers.Tests;

/// <summary>Unit tests for SES1309 (an XSLT stylesheet must not be loaded with script execution enabled).</summary>
public class XsltScriptExecutionAnalyzerUnitTest
{
    /// <summary>The cached core references used by alternate XSLT framework surfaces.</summary>
    private static readonly ImmutableArray<MetadataReference> CoreReferences = [RuntimeMetadataReferences.CoreLibrary];

    /// <summary>Verifies an object initializer that sets <c>EnableScript = true</c> is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task ObjectInitializerEnableScriptTrueReportedAsync() =>
        VerifyNet90Async(
            """
            using System.Xml.Xsl;

            public class C
            {
                public void M()
                {
                    var transform = new XslCompiledTransform();
                    transform.Load("style.xslt", {|SES1309:new XsltSettings { EnableScript = true }|}, null);
                }
            }
            """);

    /// <summary>Verifies the constructor with a constant <c>enableScript</c> of <c>true</c> is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task ConstructorEnableScriptTrueReportedAsync() =>
        VerifyNet90Async(
            """
            using System.Xml.Xsl;

            public class C
            {
                public void M()
                {
                    var transform = new XslCompiledTransform();
                    transform.Load("style.xslt", {|SES1309:new XsltSettings(false, true)|}, null);
                }
            }
            """);

    /// <summary>Verifies a reordered named <c>enableScript: true</c> constructor argument is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task NamedConstructorEnableScriptArgumentReportedAsync() =>
        VerifyNet90Async(
            """
            using System.Xml.Xsl;

            public class C
            {
                public void M()
                {
                    var transform = new XslCompiledTransform();
                    transform.Load("style.xslt", {|SES1309:new XsltSettings(enableScript: true, enableDocumentFunction: false)|}, null);
                }
            }
            """);

    /// <summary>Verifies the static <c>XsltSettings.TrustedXslt</c> (which enables script) is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task TrustedXsltStaticReportedAsync() =>
        VerifyNet90Async(
            """
            using System.Xml.Xsl;

            public class C
            {
                public void M()
                {
                    var transform = new XslCompiledTransform();
                    transform.Load("style.xslt", {|SES1309:XsltSettings.TrustedXslt|}, null);
                }
            }
            """);

    /// <summary>Verifies the settings passed by name in a reordered call are still reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task NamedSettingsArgumentReportedAsync() =>
        VerifyNet90Async(
            """
            using System.Xml.Xsl;

            public class C
            {
                public void M()
                {
                    var transform = new XslCompiledTransform();
                    transform.Load(stylesheetUri: "style.xslt", stylesheetResolver: null, settings: {|SES1309:XsltSettings.TrustedXslt|});
                }
            }
            """);

    /// <summary>Verifies the static <c>XsltSettings.Default</c> (script disabled) is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task DefaultSettingsIsCleanAsync() =>
        VerifyNet90Async(
            """
            using System.Xml.Xsl;

            public class C
            {
                public void M()
                {
                    var transform = new XslCompiledTransform();
                    transform.Load("style.xslt", XsltSettings.Default, null);
                }
            }
            """);

    /// <summary>Verifies an initializer that sets <c>EnableScript = false</c> is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task InitializerEnableScriptFalseIsCleanAsync() =>
        VerifyNet90Async(
            """
            using System.Xml.Xsl;

            public class C
            {
                public void M()
                {
                    var transform = new XslCompiledTransform();
                    transform.Load("style.xslt", new XsltSettings { EnableScript = false }, null);
                }
            }
            """);

    /// <summary>Verifies a constructor whose <c>enableScript</c> argument is <c>false</c> is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task ConstructorEnableScriptFalseIsCleanAsync() =>
        VerifyNet90Async(
            """
            using System.Xml.Xsl;

            public class C
            {
                public void M()
                {
                    var transform = new XslCompiledTransform();
                    transform.Load("style.xslt", new XsltSettings(true, false), null);
                }
            }
            """);

    /// <summary>Verifies an initializer that enables only the document() function is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task DocumentFunctionOnlyIsCleanAsync() =>
        VerifyNet90Async(
            """
            using System.Xml.Xsl;

            public class C
            {
                public void M()
                {
                    var transform = new XslCompiledTransform();
                    transform.Load("style.xslt", new XsltSettings { EnableDocumentFunction = true }, null);
                }
            }
            """);

    /// <summary>Verifies settings first stored in a local and then passed are not reported (local shape only).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task SettingsFromLocalIsCleanAsync() =>
        VerifyNet90Async(
            """
            using System.Xml.Xsl;

            public class C
            {
                public void M()
                {
                    var settings = new XsltSettings { EnableScript = true };
                    var transform = new XslCompiledTransform();
                    transform.Load("style.xslt", settings, null);
                }
            }
            """);

    /// <summary>Verifies a single-argument <c>Load</c> with no settings is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task LoadWithoutSettingsIsCleanAsync() =>
        VerifyNet90Async(
            """
            using System.Xml.Xsl;

            public class C
            {
                public void M()
                {
                    var transform = new XslCompiledTransform();
                    transform.Load("style.xslt");
                }
            }
            """);

    /// <summary>Verifies a same-named <c>Load</c>/<c>XsltSettings</c> on unrelated types is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task UnrelatedTypesWithMatchingShapeIsCleanAsync() =>
        VerifyNet90Async(
            """
            public sealed class XsltSettings
            {
                public bool EnableScript { get; set; }
            }

            public sealed class XslCompiledTransform
            {
                public void Load(string uri, XsltSettings settings, object resolver)
                {
                }
            }

            public class C
            {
                public void M()
                {
                    var transform = new XslCompiledTransform();
                    transform.Load("style.xslt", new XsltSettings { EnableScript = true }, null);
                }
            }
            """);

    /// <summary>Verifies transparent wrappers preserve the inline settings diagnostic.</summary>
    /// <param name="expression">The wrapped settings expression with its expected diagnostic.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("({|SES1309:new XsltSettings(false, true)|})")]
    [Arguments("checked({|SES1309:new XsltSettings(false, true)|})")]
    [Arguments("unchecked({|SES1309:new XsltSettings(false, true)|})")]
    [Arguments("{|SES1309:new XsltSettings(false, true)|}!")]
    [Arguments("{|SES1309:TrustedXslt|}")]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task WrappedAndImportedSettingsAreReportedAsync(string expression) =>
        VerifyNet90Async(
            $$"""
            #nullable enable
            using System.Xml.Xsl;
            using static System.Xml.Xsl.XsltSettings;
            class C
            {
                void M(XslCompiledTransform transform) => transform.Load("style.xslt", {{expression}}, null);
            }
            """);

    /// <summary>Verifies target-typed settings construction currently produces no diagnostic.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task TargetTypedSettingsAreCurrentlyCleanAsync() =>
        VerifyNet90Async(
            """
            using System.Xml.Xsl;
            class C
            {
                void M(XslCompiledTransform transform) => transform.Load("style.xslt", new(false, true), null);
            }
            """);

    /// <summary>Verifies nonconstant script flags and unrelated settings members remain clean.</summary>
    /// <param name="expression">The settings expression that does not prove script is enabled.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("new XsltSettings(false, enabled)")]
    [Arguments("new XsltSettings { EnableScript = enabled }")]
    [Arguments("new XsltSettings { }")]
    [Arguments("new XsltSettings()")]
    [Arguments("new XsltSettings { EnableDocumentFunction = false, EnableScript = false }")]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task SettingsWithoutConstantTrueAreCleanAsync(string expression) =>
        VerifyNet90Async(
            $$"""
            using System.Xml.Xsl;
            class C
            {
                void M(XslCompiledTransform transform, bool enabled) => transform.Load("style.xslt", {{expression}}, null);
            }
            """);

    /// <summary>Verifies a true constructor flag currently reports even if an initializer later disables script.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task ConstructorTrueWithInitializerFalseIsReportedAsync() =>
        VerifyNet90Async(
            """
            using System.Xml.Xsl;
            class C
            {
                void M(XslCompiledTransform transform) =>
                    transform.Load("style.xslt", {|SES1309:new XsltSettings(false, true) { EnableScript = false }|}, null);
            }
            """);

    /// <summary>Verifies each script-enabling Load call is reported independently.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task RepeatedScriptEnablingCallsAreReportedAsync() =>
        VerifyNet90Async(
            """
            using System.Xml.Xsl;
            class C
            {
                void M(XslCompiledTransform transform)
                {
                    transform.Load("first.xslt", {|SES1309:new XsltSettings(false, true)|}, null);
                    transform.Load("second.xslt", {|SES1309:XsltSettings.TrustedXslt|}, null);
                }
            }
            """);

    /// <summary>Verifies other argument constructions do not make safe or locally stored settings script-enabling.</summary>
    /// <param name="expression">The settings variable or safe framework property.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("settings")]
    [Arguments("XsltSettings.Default")]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task CandidateInResolverDoesNotEnableSafeSettingsAsync(string expression) =>
        VerifyNet90Async(
            $$"""
            using System.Xml;
            using System.Xml.Xsl;
            class C
            {
                void M(XslCompiledTransform transform, XsltSettings settings) =>
                    transform.Load("style.xslt", {{expression}}, new XmlUrlResolver());
            }
            """);

    /// <summary>Verifies a member named TrustedXslt must be the static framework settings property.</summary>
    /// <param name="member">The unrelated settings-producing member.</param>
    /// <param name="expression">The expression that refers to that member.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("public static XsltSettings TrustedXslt => XsltSettings.TrustedXslt;", "Other.TrustedXslt")]
    [Arguments("public XsltSettings TrustedXslt => XsltSettings.TrustedXslt;", "other.TrustedXslt")]
    [Arguments("public static XsltSettings TrustedXslt = XsltSettings.TrustedXslt;", "Other.TrustedXslt")]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task UnrelatedTrustedSettingsMembersAreCleanAsync(string member, string expression) =>
        VerifyNet90Async(
            $$"""
            using System.Xml.Xsl;
            class Other { {{member}} }
            class C
            {
                void M(XslCompiledTransform transform, Other other) => transform.Load("style.xslt", {{expression}}, null);
            }
            """);

    /// <summary>Verifies syntax candidates with invalid binding or delegate invocation remain clean.</summary>
    /// <param name="source">The source containing the near-miss invocation.</param>
    /// <param name="cancellationToken">The test cancellation token.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("class C { void M(System.Xml.Xsl.XslCompiledTransform t) => t.Load(42, new System.Xml.Xsl.XsltSettings(false, true)); }")]
    [Arguments("class C { System.Action<string, System.Xml.Xsl.XsltSettings> Load; void M(C t) => t.Load(\"\", new System.Xml.Xsl.XsltSettings(false, true)); }")]
    [Arguments("class C { void M(System.Xml.Xsl.XslCompiledTransform t) => t.Load(\"\", Missing.TrustedXslt, null); }")]
    [Arguments("class C { void M(System.Xml.Xsl.XslCompiledTransform t, int i) => t.Load(\"\", i++, null); }")]
    [Arguments("class C { void M(System.Xml.Xsl.XslCompiledTransform t) => t.Load(\"\", (System.Xml.Xsl.XsltSettings)null, null); }")]
    public async Task UnboundAndDifferentInvocationOperationsAreCleanAsync(string source, CancellationToken cancellationToken)
    {
        var diagnostics = await AnalyzeAsync(source, RuntimeMetadataReferences.Platform, cancellationToken);
        await Assert.That(diagnostics).IsEmpty();
    }

    /// <summary>Verifies missing framework types and Load overloads without settings are ignored.</summary>
    /// <param name="framework">The available XSLT framework types.</param>
    /// <param name="cancellationToken">The test cancellation token.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("")]
    [Arguments("namespace System.Xml.Xsl { public class XslCompiledTransform { public void Load(string uri, object settings) { } } }")]
    [Arguments("namespace System.Xml.Xsl { public class XsltSettings { } public class XslCompiledTransform { public void Load(string uri, object settings) { } } }")]
    public async Task MissingFrameworkOrSettingsParameterIsCleanAsync(string framework, CancellationToken cancellationToken)
    {
        var source = $$"""{{framework}}class C { void M(System.Xml.Xsl.XslCompiledTransform t) => t.Load("", new object()); }""";
        var diagnostics = await AnalyzeAsync(source, CoreReferences, cancellationToken);
        await Assert.That(diagnostics).IsEmpty();
    }

    /// <summary>Verifies varargs without a bound settings parameter cannot enable script.</summary>
    /// <param name="cancellationToken">The test cancellation token.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task VarargsLoadWithoutSettingsParameterIsCleanAsync(CancellationToken cancellationToken)
    {
        const string Source = """
                              namespace System.Xml.Xsl
                              {
                                  public class XsltSettings { }
                                  public class XslCompiledTransform { public void Load(object uri, __arglist) { } }
                              }
                              class C { void M(System.Xml.Xsl.XslCompiledTransform t) => t.Load(new object(), __arglist(true)); }
                              """;
        var diagnostics = await AnalyzeAsync(Source, CoreReferences, cancellationToken);
        await Assert.That(diagnostics).IsEmpty();
    }

    /// <summary>Verifies alternate settings surfaces cannot enable script through fields or nonboolean constants.</summary>
    /// <param name="members">The settings stub members.</param>
    /// <param name="expression">The settings construction to analyze.</param>
    /// <param name="cancellationToken">The test cancellation token.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("public bool EnableScript;", "new XsltSettings { EnableScript = true }")]
    [Arguments("public int EnableScript { get; set; }", "new XsltSettings { EnableScript = 1 }")]
    [Arguments("public XsltSettings(int enableScript) { }", "new XsltSettings(1)")]
    [Arguments("public XsltSettings(string enableScript) { }", "new XsltSettings(null)")]
    [Arguments("public XsltSettings(__arglist) { }", "new XsltSettings(__arglist(true))")]
    [Arguments("public class Derived : XsltSettings { } public bool EnableScript { get; set; }", "new XsltSettings.Derived { EnableScript = true }")]
    [Arguments("public XsltSettings Nested => this; public bool EnableScript { get; set; }", "new XsltSettings { Nested = { EnableScript = true } }")]
    public async Task IncompatibleSettingsMembersAreCleanAsync(string members, string expression, CancellationToken cancellationToken)
    {
        var source = $$"""
                       using System.Xml.Xsl;
                       namespace System.Xml.Xsl
                       {
                           public class XsltSettings { {{members}} }
                           public class XslCompiledTransform { public void Load(string uri, XsltSettings settings) { } }
                       }
                       class C { void M(XslCompiledTransform t) => t.Load("", {{expression}}); }
                       """;
        var diagnostics = await AnalyzeAsync(source, CoreReferences, cancellationToken);
        await Assert.That(diagnostics).IsEmpty();
    }

    /// <summary>Runs an analyzer-only verification against the .NET 9 reference assemblies (where the XSLT types exist).</summary>
    /// <param name="source">The source with diagnostic markup.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task VerifyNet90Async(string source)
    {
        var test = new AnalyzeXslt.Test { ReferenceAssemblies = AnalyzerFrameworks.Net90, TestCode = source };

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Analyzes incomplete calls or deliberately restricted framework surfaces.</summary>
    /// <param name="source">The source to analyze.</param>
    /// <param name="references">The cached framework references.</param>
    /// <param name="cancellationToken">The test cancellation token.</param>
    /// <returns>The analyzer diagnostics.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Task<ImmutableArray<Diagnostic>> AnalyzeAsync(string source, ImmutableArray<MetadataReference> references, CancellationToken cancellationToken) =>
        CSharpCompilation.Create(
                "XsltSettingsTests",
                [CSharpSyntaxTree.ParseText(source, cancellationToken: cancellationToken)],
                references,
                new(OutputKind.DynamicallyLinkedLibrary))
            .WithAnalyzers([new Ses1309XsltScriptExecutionAnalyzer()])
            .GetAnalyzerDiagnosticsAsync(cancellationToken);
}
