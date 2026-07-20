// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Testing;

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
    [Test]
    public async Task LiteralTrustServerCertificateToConnectionReportedAsync()
        => await VerifyAsync(
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
    [Test]
    public async Task LiteralEncryptFalseToLegacyConnectionReportedAsync()
        => await VerifyAsync(
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
    [Test]
    public async Task LiteralEncryptOptionalToBuilderReportedAsync()
        => await VerifyAsync(
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
    [Test]
    public async Task LiteralAssignedToConnectionStringReportedAsync()
        => await VerifyAsync(
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
    [Test]
    public async Task LiteralWithSpacesAndMixedCaseReportedAsync()
        => await VerifyAsync(
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
    [Test]
    public async Task NamedConnectionStringArgumentReportedAsync()
        => await VerifyAsync(
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
    [Test]
    public async Task BuilderInitializerTrustServerCertificateReportedAsync()
        => await VerifyAsync(
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
    [Test]
    public async Task BuilderInitializerEncryptFalseReportedAsync()
        => await VerifyAsync(
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
    [Test]
    public async Task BuilderInitializerEncryptOptionalReportedAsync()
        => await VerifyAsync(
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
    [Test]
    public async Task BuilderPropertyAssignmentReportedAsync()
        => await VerifyAsync(
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
    [Test]
    public async Task SecureLiteralIsCleanAsync()
        => await VerifyAsync(
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
    [Test]
    public async Task BuilderStrictEncryptIsCleanAsync()
        => await VerifyAsync(
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
    [Test]
    public async Task NonConstantTrustServerCertificateIsCleanAsync()
        => await VerifyAsync(
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
    [Test]
    public async Task ConnectionStringFromVariableIsCleanAsync()
        => await VerifyAsync(
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
    [Test]
    public async Task UnrelatedTypeMembersAreCleanAsync()
        => await VerifyAsync(
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

        var test = new AnalyzeSqlTransport.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = Source,
        };

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Runs an analyzer-only verification with the SQL client stubs in scope.</summary>
    /// <param name="body">The test source with diagnostic markup.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task VerifyAsync(string body)
    {
        var test = new AnalyzeSqlTransport.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = SqlClientStubs + body,
        };

        await test.RunAsync(CancellationToken.None);
    }
}
