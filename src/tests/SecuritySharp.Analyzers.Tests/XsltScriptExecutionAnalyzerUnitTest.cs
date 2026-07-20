// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Testing;

using AnalyzeXslt = SecuritySharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    SecuritySharp.Analyzers.Ses1309XsltScriptExecutionAnalyzer>;

namespace SecuritySharp.Analyzers.Tests;

/// <summary>Unit tests for SES1309 (an XSLT stylesheet must not be loaded with script execution enabled).</summary>
public class XsltScriptExecutionAnalyzerUnitTest
{
    /// <summary>Verifies an object initializer that sets <c>EnableScript = true</c> is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ObjectInitializerEnableScriptTrueReportedAsync()
        => await VerifyNet90Async(
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
    [Test]
    public async Task ConstructorEnableScriptTrueReportedAsync()
        => await VerifyNet90Async(
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
    [Test]
    public async Task NamedConstructorEnableScriptArgumentReportedAsync()
        => await VerifyNet90Async(
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
    [Test]
    public async Task TrustedXsltStaticReportedAsync()
        => await VerifyNet90Async(
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
    [Test]
    public async Task NamedSettingsArgumentReportedAsync()
        => await VerifyNet90Async(
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
    [Test]
    public async Task DefaultSettingsIsCleanAsync()
        => await VerifyNet90Async(
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
    [Test]
    public async Task InitializerEnableScriptFalseIsCleanAsync()
        => await VerifyNet90Async(
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
    [Test]
    public async Task ConstructorEnableScriptFalseIsCleanAsync()
        => await VerifyNet90Async(
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
    [Test]
    public async Task DocumentFunctionOnlyIsCleanAsync()
        => await VerifyNet90Async(
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
    [Test]
    public async Task SettingsFromLocalIsCleanAsync()
        => await VerifyNet90Async(
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
    [Test]
    public async Task LoadWithoutSettingsIsCleanAsync()
        => await VerifyNet90Async(
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
    [Test]
    public async Task UnrelatedTypesWithMatchingShapeIsCleanAsync()
        => await VerifyNet90Async(
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

    /// <summary>Runs an analyzer-only verification against the .NET 9 reference assemblies (where the XSLT types exist).</summary>
    /// <param name="source">The source with diagnostic markup.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task VerifyNet90Async(string source)
    {
        var test = new AnalyzeXslt.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = source
        };

        await test.RunAsync(CancellationToken.None);
    }
}
