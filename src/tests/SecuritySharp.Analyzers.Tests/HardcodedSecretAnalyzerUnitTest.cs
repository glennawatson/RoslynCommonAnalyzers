// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Testing;

using AnalyzeSecret = SecuritySharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    SecuritySharp.Analyzers.Ses1201HardcodedSecretAnalyzer>;

namespace SecuritySharp.Analyzers.Tests;

/// <summary>Unit tests for SES1201 (a string literal must not hard-code a recognisable secret).</summary>
public class HardcodedSecretAnalyzerUnitTest
{
    /// <summary>Verifies each recognised credential shape is classified with the expected kind label.</summary>
    /// <param name="value">The decoded literal content.</param>
    /// <param name="expectedKind">The kind label the classifier is expected to return.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    [Arguments("sk-1A2b3C4d5E6f7G8h9I0j", HardcodedSecretClassifier.OpenAiApiKey)]
    [Arguments("AKIAIOSFODNN7EXAMPLE", HardcodedSecretClassifier.AwsAccessKeyId)]
    [Arguments("ghp_0123456789abcdefghijABCDEFGHIJklmnop", HardcodedSecretClassifier.GitHubToken)]
    [Arguments("gho_0123456789abcdefghijABCDEFGHIJklmnop", HardcodedSecretClassifier.GitHubToken)]
    [Arguments("AIza0123456789abcdefghijklmnopqrstABCDE", HardcodedSecretClassifier.GoogleApiKey)]
    [Arguments("-----BEGIN PRIVATE KEY-----\nMIIabcDEF123\n-----END PRIVATE KEY-----", HardcodedSecretClassifier.PrivateKey)]
    [Arguments("-----BEGIN RSA PRIVATE KEY-----\nMIIabcDEF123\n-----END RSA PRIVATE KEY-----", HardcodedSecretClassifier.PrivateKey)]
    [Arguments(
        "Endpoint=sb://ns.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=abcdefghijklmnopqrstuvwxyz0123456789ABCDEF=",
        HardcodedSecretClassifier.AzureAccessKey)]
    [Arguments(
        "DefaultEndpointsProtocol=https;AccountName=devstoreaccount1;AccountKey=Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tqK1SZFPTOtrKBHBeksoGMGw==;",
        HardcodedSecretClassifier.AzureAccessKey)]
    [Arguments("Server=tcp:myserver.database.windows.net;Database=mydb;User ID=sa;Password=Str0ng2Passw0rd;", HardcodedSecretClassifier.ConnectionStringPassword)]
    [Arguments("Data Source=.;Initial Catalog=Sales;Integrated Security=false;Pwd=W1nter2024Rocks;", HardcodedSecretClassifier.ConnectionStringPassword)]
    [Arguments("Host=db.example.com;Username=admin;Password=SuperHost99Pass", HardcodedSecretClassifier.ConnectionStringPassword)]
    [Arguments("Initial Catalog=Store;Password=Cat4logPass99", HardcodedSecretClassifier.ConnectionStringPassword)]
    public async Task ClassifiesRecognisedSecretAsync(string value, string expectedKind)
        => await Assert.That(HardcodedSecretClassifier.Classify(value)).IsEqualTo(expectedKind);

    /// <summary>Verifies Slack token shapes are classified, with prefix and body supplied separately so no contiguous token literal appears in the source.</summary>
    /// <param name="prefix">The Slack token type prefix.</param>
    /// <param name="body">The token body following the prefix.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    [Arguments("xoxb-", "123456789012-1234567890123456-abcdEFGHijklMNOP")]
    [Arguments("xoxp-", "987654321098-0192837465019-ZYXWvutsRQPOnmlk")]
    public async Task ClassifiesSlackTokenAsync(string prefix, string body)
        => await Assert.That(HardcodedSecretClassifier.Classify(prefix + body)).IsEqualTo(HardcodedSecretClassifier.SlackToken);

    /// <summary>Verifies ordinary text, placeholders, and near-misses are not classified as secrets.</summary>
    /// <param name="value">The decoded literal content.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    [Arguments("This is a perfectly normal comment string")]
    [Arguments("short")]
    [Arguments("sunny day, a long sentence with no key")]
    [Arguments("Application configuration values live here")]
    [Arguments("good morning to everyone reading this")]
    [Arguments("xml document content that is not a token")]
    [Arguments("sk-xxxxxxxxxxxxxxxxxxxxxxxx")]
    [Arguments("AKIAXXXXXXXXXXXXXXXX")]
    [Arguments("AKIAABABABABABABABAB")]
    [Arguments("<your-api-key-here-goes-in-config>")]
    [Arguments("comparison uses a < sign in this sentence")]
    [Arguments("ghp_short")]
    [Arguments("AKIASHORTVALUE not a key")]
    [Arguments("AIzaShortValue not a key here")]
    [Arguments("ghp_shortbody not a real token")]
    [Arguments("xoxb-short not a real token here")]
    [Arguments("aGVsbG8gd29ybGQgdGhpcyBpcyE=")]
    [Arguments("12345678-90ab-cdef-1234-567890abcdef")]
    [Arguments("-----BEGIN CERTIFICATE-----MIIabc-----END CERTIFICATE-----")]
    [Arguments("server=localhost;password=secretvalue")]
    [Arguments("AccountKey=short;Endpoint=x")]
    [Arguments("sk-cafe latte order for the whole team")]
    [Arguments("Server=x;Database=y;Password=abc")]
    [Arguments("Server=localhost;Database=mydb;Integrated Security=true")]
    [Arguments("Server=localhost;Database=db;Password=changeme")]
    [Arguments("Server=localhost;Database=db;Password=xxxxxx")]
    [Arguments("Note Password=Hidden123Value written in prose")]
    [Arguments("PRIVATE KEY----- appears before -----BEGIN in this note")]
    public async Task LeavesNonSecretsUnclassifiedAsync(string value)
        => await Assert.That(HardcodedSecretClassifier.Classify(value)).IsNull();

    /// <summary>Verifies a non-ASCII character following a prefix stops the class scan without a match.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task StopsClassScanAtNonAsciiCharacterAsync()
        => await Assert.That(HardcodedSecretClassifier.Classify("sk-abéghijklmnopqrstuvwxyz012345")).IsNull();

    /// <summary>Verifies the analyzer reports a hard-coded key literal in real source.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ReportsHardcodedKeyLiteralAsync()
        => await VerifyAsync(
            """
            public class C
            {
                public string GetKey() => {|SES1201:"sk-1A2b3C4d5E6f7G8h9I0j"|};
            }
            """);

    /// <summary>Verifies the analyzer reports a hard-coded connection-string password literal.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ReportsHardcodedConnectionStringAsync()
        => await VerifyAsync(
            """
            public class C
            {
                public string GetConnection() => {|SES1201:"Server=db;Database=app;User ID=sa;Password=Str0ng2Passw0rd"|};
            }
            """);

    /// <summary>Verifies the analyzer stays silent on ordinary string literals.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task LeavesOrdinaryLiteralAloneAsync()
        => await VerifyAsync(
            """
            public class C
            {
                public string GetGreeting() => "hello, this is an ordinary message";
            }
            """);

    /// <summary>Runs an analyzer-only verification against the .NET 9 reference assemblies.</summary>
    /// <param name="source">The source with diagnostic markup.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task VerifyAsync(string source)
    {
        var test = new AnalyzeSecret.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = source
        };

        await test.RunAsync(CancellationToken.None);
    }
}
