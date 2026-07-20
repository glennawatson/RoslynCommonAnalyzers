// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Testing;

using AnalyzeUntrustedXmlSignatureKey = SecuritySharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    SecuritySharp.Analyzers.Ses1008UntrustedXmlSignatureKeyAnalyzer>;

namespace SecuritySharp.Analyzers.Tests;

/// <summary>Unit tests for SES1008 (an XML signature must be verified against a known key).</summary>
public class UntrustedXmlSignatureKeyAnalyzerUnitTest
{
    /// <summary>
    /// A source-declared stand-in for <c>System.Security.Cryptography.Xml.SignedXml</c>. The XML-signing
    /// types ship in a separate package that the reference-assembly sets used here do not carry, so the
    /// type is declared in source under its real metadata name (which the rule resolves the same way) with
    /// the four <c>CheckSignature</c> overloads the rule distinguishes: the no-key parameterless and
    /// single-<c>bool</c> forms it reports, and the key/certificate forms it leaves alone.
    /// </summary>
    private const string SignedXmlStub = """

                                         namespace System.Security.Cryptography.Xml
                                         {
                                             public class SignedXml
                                             {
                                                 public bool CheckSignature() => true;

                                                 public bool CheckSignature(bool refProcessing) => true;

                                                 public bool CheckSignature(System.Security.Cryptography.AsymmetricAlgorithm key) => true;

                                                 public bool CheckSignature(System.Security.Cryptography.X509Certificates.X509Certificate2 certificate, bool verifySignatureOnly) => true;
                                             }
                                         }
                                         """;

    /// <summary>Verifies the parameterless <c>CheckSignature()</c> is reported with the receiver name.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ParameterlessCheckSignatureReportedAsync()
        => await VerifyReportedAsync(
            """
            using System.Security.Cryptography.Xml;

            public class C
            {
                public bool Verify(SignedXml signedXml) => {|SES1008:signedXml.CheckSignature()|};
            }
            """);

    /// <summary>Verifies the single-<c>bool</c> <c>CheckSignature(bool)</c> is reported (it still supplies no key).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task BoolCheckSignatureReportedAsync()
        => await VerifyReportedAsync(
            """
            using System.Security.Cryptography.Xml;

            public class C
            {
                public bool Verify(SignedXml signedXml) => {|SES1008:signedXml.CheckSignature(true)|};
            }
            """);

    /// <summary>Verifies a null-conditional <c>signedXml?.CheckSignature()</c> is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ConditionalAccessCheckSignatureReportedAsync()
        => await VerifyReportedAsync(
            """
            using System.Security.Cryptography.Xml;

            public class C
            {
                public bool Verify(SignedXml signedXml) => signedXml?{|SES1008:.CheckSignature()|} ?? false;
            }
            """);

    /// <summary>Verifies a call through a member-access receiver reports with the receiver member's name.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task MemberAccessReceiverCheckSignatureReportedAsync()
        => await VerifyReportedAsync(
            """
            using System.Security.Cryptography.Xml;

            public class Holder
            {
                public SignedXml Signed { get; set; }
            }

            public class C
            {
                public bool Verify(Holder holder) => {|SES1008:holder.Signed.CheckSignature()|};
            }
            """);

    /// <summary>Verifies an unqualified inherited <c>CheckSignature()</c> inside a <c>SignedXml</c> subclass is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task InheritedBareCheckSignatureReportedAsync()
        => await VerifyReportedAsync(
            """
            public class MySigned : System.Security.Cryptography.Xml.SignedXml
            {
                public bool Run() => {|SES1008:CheckSignature()|};
            }
            """);

    /// <summary>Verifies the key overload <c>CheckSignature(AsymmetricAlgorithm)</c> is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task KeyOverloadCleanAsync()
        => await VerifyCleanAsync(
            """
            using System.Security.Cryptography;
            using System.Security.Cryptography.Xml;

            public class C
            {
                public bool Verify(SignedXml signedXml, AsymmetricAlgorithm key) => signedXml.CheckSignature(key);
            }
            """);

    /// <summary>Verifies the certificate overload <c>CheckSignature(X509Certificate2, bool)</c> is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task CertificateOverloadCleanAsync()
        => await VerifyCleanAsync(
            """
            using System.Security.Cryptography.X509Certificates;
            using System.Security.Cryptography.Xml;

            public class C
            {
                public bool Verify(SignedXml signedXml, X509Certificate2 certificate) => signedXml.CheckSignature(certificate, false);
            }
            """);

    /// <summary>Verifies a same-named <c>CheckSignature()</c> on an unrelated type is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task UnrelatedCheckSignatureCleanAsync()
        => await VerifyCleanAsync(
            """
            public class Unrelated
            {
                public bool CheckSignature() => true;
            }

            public class C
            {
                public bool Verify(Unrelated other) => other.CheckSignature();
            }
            """);

    /// <summary>Verifies a delegate field named <c>CheckSignature</c> invoked as a delegate is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task DelegateFieldNamedCheckSignatureCleanAsync()
        => await VerifyCleanAsync(
            """
            using System;

            public class Holder
            {
                public Func<bool> CheckSignature = () => true;
            }

            public class C
            {
                public bool Verify(Holder holder) => holder.CheckSignature();
            }
            """);

    /// <summary>Verifies an invocation with no simple callee name (a returned delegate invoked inline) is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task InvokedDelegateResultCleanAsync()
        => await VerifyCleanAsync(
            """
            using System;

            public class C
            {
                private static Func<bool> Make() => () => true;

                public bool Verify() => Make()();
            }
            """);

    /// <summary>Verifies the rule stays silent when <c>SignedXml</c> is absent from the compilation.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SilentWhenSignedXmlAbsentAsync()
    {
        const string Source = """
                              public class Fake
                              {
                                  public bool CheckSignature() => true;
                              }

                              public class C
                              {
                                  public bool Verify(Fake fake) => fake.CheckSignature();
                              }
                              """;

        var test = new AnalyzeUntrustedXmlSignatureKey.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = Source,
        };

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Runs a verification for a source that carries a reported no-key <c>CheckSignature</c> call.</summary>
    /// <param name="source">The source with diagnostic markup; the stubbed <c>SignedXml</c> is appended.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static Task VerifyReportedAsync(string source) => RunAsync(source + SignedXmlStub);

    /// <summary>Runs a verification for a source the rule must leave unreported.</summary>
    /// <param name="source">The source; the stubbed <c>SignedXml</c> is appended.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static Task VerifyCleanAsync(string source) => RunAsync(source + SignedXmlStub);

    /// <summary>Runs the analyzer against the .NET 9 reference assemblies with the supplied source.</summary>
    /// <param name="source">The full source to analyze.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task RunAsync(string source)
    {
        var test = new AnalyzeUntrustedXmlSignatureKey.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = source,
        };

        await test.RunAsync(CancellationToken.None);
    }
}
