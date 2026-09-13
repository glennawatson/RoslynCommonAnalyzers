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

using AnalyzeSqlTransport = SecuritySharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    SecuritySharp.Analyzers.Ses1107WeakenedSqlTransportSecurityAnalyzer>;

namespace SecuritySharp.Analyzers.Tests;

/// <summary>Unit tests for SES1107 (a SQL connection must not weaken transport security).</summary>
public class WeakenedSqlTransportSecurityAnalyzerUnitTest
{
    /// <summary>Minimal source-declared stubs for the SQL client types the rule gates on.</summary>
    private const string SqlClientStubs =
        """
        namespace Microsoft.Data.SqlClient
        {
            public sealed class SqlConnectionEncryptOption
            {
                public static SqlConnectionEncryptOption Optional { get; } = new SqlConnectionEncryptOption();
                public static SqlConnectionEncryptOption Mandatory { get; } = new SqlConnectionEncryptOption();
                public static SqlConnectionEncryptOption Strict { get; } = new SqlConnectionEncryptOption();
                public static implicit operator SqlConnectionEncryptOption(bool value) => value ? Mandatory : Optional;
            }

            public sealed class SqlConnection
            {
                public SqlConnection() { }
                public SqlConnection(string connectionString) { }
                public string ConnectionString { get; set; }
            }

            public sealed class SqlConnectionStringBuilder
            {
                public SqlConnectionStringBuilder() { }
                public SqlConnectionStringBuilder(string connectionString) { }
                public string ConnectionString { get; set; }
                public string DataSource { get; set; }
                public bool TrustServerCertificate { get; set; }
                public SqlConnectionEncryptOption Encrypt { get; set; }
            }
        }

        namespace System.Data.SqlClient
        {
            public sealed class SqlConnection
            {
                public SqlConnection() { }
                public SqlConnection(string connectionString) { }
                public string ConnectionString { get; set; }
            }

            public sealed class SqlConnectionStringBuilder
            {
                public SqlConnectionStringBuilder() { }
                public SqlConnectionStringBuilder(string connectionString) { }
                public string ConnectionString { get; set; }
                public bool TrustServerCertificate { get; set; }
                public bool Encrypt { get; set; }
            }
        }

        """;

    /// <summary>Verifies a literal <c>TrustServerCertificate=true</c> passed to a modern SqlConnection is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task LiteralTrustServerCertificateToConnectionReportedAsync() =>
        VerifyAsync(
            """
            public class C
            {
                public void M()
                {
                    var connection = new Microsoft.Data.SqlClient.SqlConnection({|SES1107:"Server=db;Database=app;User Id=sa;Password=p;TrustServerCertificate=true"|});
                }
            }
            """);

    /// <summary>Verifies a literal <c>Encrypt=false</c> passed to a legacy SqlConnection is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task LiteralEncryptFalseToLegacyConnectionReportedAsync() =>
        VerifyAsync(
            """
            public class C
            {
                public void M()
                {
                    var connection = new System.Data.SqlClient.SqlConnection({|SES1107:"Server=db;Database=app;Encrypt=false"|});
                }
            }
            """);

    /// <summary>Verifies a literal <c>Encrypt=Optional</c> passed to a builder constructor is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task LiteralEncryptOptionalToBuilderReportedAsync() =>
        VerifyAsync(
            """
            public class C
            {
                public void M()
                {
                    var builder = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder({|SES1107:"Server=db;Encrypt=Optional"|});
                }
            }
            """);

    /// <summary>Verifies a weakening literal assigned to the <c>ConnectionString</c> property is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task LiteralAssignedToConnectionStringReportedAsync() =>
        VerifyAsync(
            """
            public class C
            {
                public void M()
                {
                    var connection = new Microsoft.Data.SqlClient.SqlConnection();
                    connection.ConnectionString = {|SES1107:"Server=db;TrustServerCertificate=true"|};
                }
            }
            """);

    /// <summary>Verifies a weakening literal with spaces around the keyword is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task LiteralWithSpacesAndMixedCaseReportedAsync() =>
        VerifyAsync(
            """
            public class C
            {
                public void M()
                {
                    var connection = new Microsoft.Data.SqlClient.SqlConnection({|SES1107:"Server=db; trustservercertificate = TRUE "|});
                }
            }
            """);

    /// <summary>Verifies a weakening literal passed by the named constructor argument is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task NamedConnectionStringArgumentReportedAsync() =>
        VerifyAsync(
            """
            public class C
            {
                public void M()
                {
                    var connection = new Microsoft.Data.SqlClient.SqlConnection(connectionString: {|SES1107:"Server=db;Encrypt=no"|});
                }
            }
            """);

    /// <summary>Verifies a builder initializer that sets <c>TrustServerCertificate = true</c> is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task BuilderInitializerTrustServerCertificateReportedAsync() =>
        VerifyAsync(
            """
            public class C
            {
                public void M()
                {
                    var builder = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder
                    {
                        DataSource = "db",
                        {|SES1107:TrustServerCertificate = true|},
                    };
                }
            }
            """);

    /// <summary>Verifies a builder initializer that sets <c>Encrypt = false</c> is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task BuilderInitializerEncryptFalseReportedAsync() =>
        VerifyAsync(
            """
            public class C
            {
                public void M()
                {
                    var builder = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder
                    {
                        {|SES1107:Encrypt = false|},
                    };
                }
            }
            """);

    /// <summary>Verifies a builder initializer that sets <c>Encrypt = SqlConnectionEncryptOption.Optional</c> is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task BuilderInitializerEncryptOptionalReportedAsync() =>
        VerifyAsync(
            """
            public class C
            {
                public void M()
                {
                    var builder = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder
                    {
                        {|SES1107:Encrypt = Microsoft.Data.SqlClient.SqlConnectionEncryptOption.Optional|},
                    };
                }
            }
            """);

    /// <summary>Verifies a builder property assignment statement setting <c>TrustServerCertificate = true</c> is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task BuilderPropertyAssignmentReportedAsync() =>
        VerifyAsync(
            """
            public class C
            {
                public void M()
                {
                    var builder = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder();
                    {|SES1107:builder.TrustServerCertificate = true|};
                }
            }
            """);

    /// <summary>Verifies a secure literal (encryption on, certificate validated) is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task SecureLiteralIsCleanAsync() =>
        VerifyAsync(
            """
            public class C
            {
                public void M()
                {
                    var connection = new Microsoft.Data.SqlClient.SqlConnection("Server=db;Encrypt=true;TrustServerCertificate=false");
                }
            }
            """);

    /// <summary>Verifies a builder set to <c>Encrypt = SqlConnectionEncryptOption.Strict</c> is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task BuilderStrictEncryptIsCleanAsync() =>
        VerifyAsync(
            """
            public class C
            {
                public void M()
                {
                    var builder = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder
                    {
                        Encrypt = Microsoft.Data.SqlClient.SqlConnectionEncryptOption.Strict,
                    };
                }
            }
            """);

    /// <summary>Verifies a non-constant <c>TrustServerCertificate</c> value is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task NonConstantTrustServerCertificateIsCleanAsync() =>
        VerifyAsync(
            """
            public class C
            {
                public void M(bool isDevelopment)
                {
                    var builder = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder();
                    builder.TrustServerCertificate = isDevelopment;
                }
            }
            """);

    /// <summary>Verifies a connection string held in a variable (not a literal at the call site) is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task ConnectionStringFromVariableIsCleanAsync() =>
        VerifyAsync(
            """
            public class C
            {
                public void M()
                {
                    string value = "Server=db;TrustServerCertificate=true";
                    var connection = new Microsoft.Data.SqlClient.SqlConnection(value);
                }
            }
            """);

    /// <summary>Verifies an unrelated type carrying same-named members is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task UnrelatedTypeMembersAreCleanAsync() =>
        VerifyAsync(
            """
            public sealed class FakeOptions
            {
                public bool TrustServerCertificate { get; set; }
                public bool Encrypt { get; set; }
            }

            public class C
            {
                public void M()
                {
                    var options = new FakeOptions
                    {
                        TrustServerCertificate = true,
                        Encrypt = false,
                    };
                }
            }
            """);

    /// <summary>Verifies the rule stays silent when no gated SQL connection type is present.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SilentWhenSqlClientUnavailableAsync()
    {
        const string Source = """
                              public sealed class SqlConnection
                              {
                                  public SqlConnection(string connectionString) { }
                              }

                              public class C
                              {
                                  public void M()
                                  {
                                      var connection = new SqlConnection("Server=db;TrustServerCertificate=true");
                                  }
                              }
                              """;

        var test = new AnalyzeSqlTransport.Test { ReferenceAssemblies = ReferenceAssemblies.Net.Net90, TestCode = Source, };

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies malformed segments and secure keyword values remain silent.</summary>
    /// <param name="connectionString">The literal connection-string contents.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("")]
    [Arguments("Server")]
    [Arguments(";broken;Server=db;;")]
    [Arguments("Encrypt=")]
    [Arguments("Encrypt=   ")]
    [Arguments("   =false")]
    [Arguments("TrustServerCertificate=no")]
    [Arguments("TrustServerCertificate=truEish")]
    [Arguments("Encrypt=Mandatory")]
    [Arguments("Encrypt=STRICT")]
    [Arguments("Encrypt=fals@")]
    [Arguments("Encrypt=fals[")]
    public Task SecureAndMalformedLiteralSegmentsAreCleanAsync(string connectionString)
    {
        var source = $$"""
                       class C
                       {
                           void M()
                           {
                               var connection = new Microsoft.Data.SqlClient.SqlConnection("{{connectionString}}");
                           }
                       }
                       """;
        return VerifyAsync(source);
    }

    /// <summary>Verifies scanning continues past empty and malformed pairs to a weakening setting.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task LaterTruthySettingInImplicitCreationIsReportedAsync() =>
        VerifyAsync(
            """
            class C
            {
                Microsoft.Data.SqlClient.SqlConnection M() => new({|SES1107:";broken;;TrustServerCertificate=YeS;Encrypt=true"|});
            }
            """);

    /// <summary>Verifies legacy builders and implicit initializers use the same security checks.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task LegacyBuilderAndImplicitInitializerAreReportedAsync() =>
        VerifyAsync(
            """
            class C
            {
                void M()
                {
                    var legacy = new System.Data.SqlClient.SqlConnectionStringBuilder({|SES1107:"Encrypt=false"|});
                    {|SES1107:legacy.Encrypt = false|};
                    Microsoft.Data.SqlClient.SqlConnection connection = new()
                    {
                        ConnectionString = {|SES1107:"Encrypt=no"|},
                    };
                    Microsoft.Data.SqlClient.SqlConnectionStringBuilder builder = new()
                    {
                        {|SES1107:TrustServerCertificate = true|},
                    };
                }
            }
            """);

    /// <summary>Verifies nonliteral assignments, secure values, and unrelated receivers remain silent.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task AssignmentNearMissesAreCleanAsync() =>
        VerifyAsync(
            """
            class Other
            {
                public Other(string text) { }
                public string ConnectionString { get; set; }
            }
            class C
            {
                void M(string text, bool encrypt, Microsoft.Data.SqlClient.SqlConnectionEncryptOption option)
                {
                    var connection = new Microsoft.Data.SqlClient.SqlConnection();
                    connection.ConnectionString = text;
                    connection.ConnectionString = "Encrypt=true";
                    var modern = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder();
                    modern.TrustServerCertificate = false;
                    modern.Encrypt = option;
                    modern.Encrypt = null;
                    modern.Encrypt = (Microsoft.Data.SqlClient.SqlConnectionEncryptOption.Optional);
                    var legacy = new System.Data.SqlClient.SqlConnectionStringBuilder();
                    legacy.Encrypt = true;
                    legacy.Encrypt = encrypt;
                    var other = new Other(text: "Encrypt=false");
                    other.ConnectionString = "Encrypt=false";
                    var positional = new Other("Encrypt=false");
                    var values = new string[1];
                    values[0] = "Encrypt=false";
                }
            }
            """);

    /// <summary>Verifies unresolved receiver types and bare locals cannot identify a SQL instance.</summary>
    /// <param name="body">The intentionally incomplete method body.</param>
    /// <param name="cancellationToken">The test cancellation token.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("string ConnectionString = null; ConnectionString = \"Encrypt=false\";")]
    [Arguments("bool Encrypt = true; Encrypt = false;")]
    [Arguments("bool Encrypt = true; bool[] values = { Encrypt = false };")]
    [Arguments("dynamic value = null; value.ConnectionString = \"Encrypt=false\";")]
    [Arguments("dynamic value = null; value.Encrypt = false;")]
    [Arguments("var value = new dynamic(\"Encrypt=false\");")]
    [Arguments("var value = new T(\"Encrypt=false\");")]
    [Arguments("var value = new Microsoft.Data.SqlClient.SqlConnection(1);")]
    [Arguments("var value = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(); value.Encrypt = Optional;")]
    [Arguments("var value = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(); int Optional = 0; value.Encrypt = Optional;")]
    public async Task UnboundSqlCandidatesAreCleanAsync(string body, CancellationToken cancellationToken)
    {
        var source = $$"""
                       {{SqlClientStubs}}
                       class C { void M<T>() { {{body}} } }
                       """;
        var diagnostics = await AnalyzeAsync(source, cancellationToken);
        await Assert.That(diagnostics).IsEmpty();
    }

    /// <summary>Verifies a statically imported Optional member still binds to the client option type.</summary>
    /// <param name="cancellationToken">The test cancellation token.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ImportedOptionalIsReportedAsync(CancellationToken cancellationToken)
    {
        const string Source = "using static Microsoft.Data.SqlClient.SqlConnectionEncryptOption;\n" + SqlClientStubs + """
                              class C
                              {
                                  void M(Microsoft.Data.SqlClient.SqlConnectionStringBuilder builder) => builder.Encrypt = Optional;
                              }
                              """;
        var diagnostics = await AnalyzeAsync(Source, cancellationToken);
        await Assert.That(diagnostics).Count().IsEqualTo(1);
        await Assert.That(diagnostics[0].Id).IsEqualTo("SES1107");
    }

    /// <summary>Verifies missing client types and absent modern option types disable only their relevant checks.</summary>
    /// <param name="declarations">The client types supplied by the compilation.</param>
    /// <param name="body">The candidate assignments.</param>
    /// <param name="cancellationToken">The test cancellation token.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("", "ConnectionString = \"Encrypt=false\"; Encrypt = false;")]
    [Arguments(
        "namespace System.Data.SqlClient { class SqlConnection { } class SqlConnectionStringBuilder { public bool Encrypt { get; set; } } }",
        "var builder = new System.Data.SqlClient.SqlConnectionStringBuilder(); builder.Encrypt = true;")]
    [Arguments("namespace Microsoft.Data.SqlClient { class SqlConnection { } }", "ConnectionString = \"Encrypt=false\"; Encrypt = false;")]
    public async Task AbsentSqlApisAreCleanAsync(string declarations, string body, CancellationToken cancellationToken)
    {
        var source = $$"""
                       {{declarations}}
                       class C
                       {
                           string ConnectionString;
                           bool Encrypt;
                           void M() { {{body}} }
                       }
                       """;
        var diagnostics = await AnalyzeAsync(source, cancellationToken);
        await Assert.That(diagnostics).IsEmpty();
    }

    /// <summary>Runs the analyzer against incomplete or alternate SQL client sources.</summary>
    /// <param name="source">The compilation source.</param>
    /// <param name="cancellationToken">The test cancellation token.</param>
    /// <returns>The analyzer diagnostics.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Task<ImmutableArray<Diagnostic>> AnalyzeAsync(string source, CancellationToken cancellationToken) =>
        CSharpCompilation.Create(
                "SqlTransport",
                [CSharpSyntaxTree.ParseText(source, cancellationToken: cancellationToken)],
                RuntimeMetadataReferences.Platform,
                new(OutputKind.DynamicallyLinkedLibrary))
            .WithAnalyzers([new Ses1107WeakenedSqlTransportSecurityAnalyzer()])
            .GetAnalyzerDiagnosticsAsync(cancellationToken);

    /// <summary>Runs an analyzer-only verification with the SQL client stubs in scope.</summary>
    /// <param name="body">The test source with diagnostic markup.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task VerifyAsync(string body)
    {
        var test = new AnalyzeSqlTransport.Test { ReferenceAssemblies = ReferenceAssemblies.Net.Net90, TestCode = SqlClientStubs + body, };

        await test.RunAsync(CancellationToken.None);
    }
}
