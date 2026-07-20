// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Testing;

using AnalyzeEmptyPassword = SecuritySharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    SecuritySharp.Analyzers.Ses1203EmptyConnectionStringPasswordAnalyzer>;

namespace SecuritySharp.Analyzers.Tests;

/// <summary>Unit tests for SES1203 (a connection string must not name a user with an empty or missing password).</summary>
public class EmptyConnectionStringPasswordAnalyzerUnitTest
{
    /// <summary>Verifies a connection string that names a user with a blank or missing password is recognised.</summary>
    /// <param name="value">The decoded literal content.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    [Arguments("Server=db;User Id=sa;Password=;")]
    [Arguments("Server=db;User Id=sa;Password=")]
    [Arguments("Server=db;User Id=sa")]
    [Arguments("Server=db;User Id=sa;")]
    [Arguments("Data Source=.;Initial Catalog=Sales;Uid=admin;Pwd=;")]
    [Arguments("Host=db.example.com;Database=app;User Id=admin")]
    [Arguments("Host=x;Uid=y")]
    [Arguments("server=db;uid=sa;pwd=")]
    [Arguments("SERVER=DB;USER ID=SA;PASSWORD=;")]
    [Arguments("Server = db ; User Id = sa ; Password = ")]
    [Arguments("Server=db;Uid=sa;Password=   ")]
    [Arguments("Server=db;Uid=sa;Password= \t\r\n ")]
    [Arguments("Server=db;User Id=sa;Integrated Security=false;Password=")]
    [Arguments("Server=db;User Id=sa;Trusted_Connection=no")]
    [Arguments("Server=db;;User Id=sa;Password=")]
    [Arguments("Server=db;Encrypt;User Id=sa;Password=")]
    [Arguments("Server=db;Encrypt=true;User Id=sa;Password=")]
    public async Task RecognisesEmptyOrMissingPasswordAsync(string value)
        => await Assert.That(EmptyConnectionStringPasswordClassifier.IsEmptyPasswordConnectionString(value)).IsTrue();

    /// <summary>Verifies a safe or unrelated literal is not recognised as an empty-password connection string.</summary>
    /// <param name="value">The decoded literal content.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    [Arguments("Server=db;User Id=sa;Password=Str0ngP4ss;")]
    [Arguments("Server=db;User Id=sa;Pwd=Str0ngP4ss")]
    [Arguments("Server=db;User Id=sa;Password=x")]
    [Arguments("Server=db;User Id=sa;Password= x ")]
    [Arguments("Server=db;User Id=sa;Integrated Security=true;Password=")]
    [Arguments("Server=db;User Id=sa;Integrated Security= SSPI ")]
    [Arguments("Server=db;User Id=sa;Trusted_Connection=true")]
    [Arguments("Server=db;User Id=sa;Trusted_Connection=yes")]
    [Arguments("Server=db;Database=app;Password=")]
    [Arguments("User Id=sa;Password=")]
    [Arguments("alpha=1;beta=2")]
    [Arguments("Serverdb;Useridsa;Passwordblank")]
    [Arguments("Server=databasewithnopassword")]
    [Arguments("Server=x;U")]
    [Arguments("hello world, this is an ordinary message")]
    public async Task LeavesSafeOrUnrelatedLiteralAsync(string value)
        => await Assert.That(EmptyConnectionStringPasswordClassifier.IsEmptyPasswordConnectionString(value)).IsFalse();

    /// <summary>Verifies the analyzer reports a connection-string literal whose password is present but empty.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ReportsEmptyPasswordLiteralAsync()
        => await VerifyAsync(
            """
            public class C
            {
                public string GetConnection() => {|SES1203:"Server=db;Database=app;User Id=sa;Password=;"|};
            }
            """);

    /// <summary>Verifies the analyzer reports a connection-string literal that names a user but has no password key.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ReportsMissingPasswordLiteralAsync()
        => await VerifyAsync(
            """
            public class C
            {
                public string GetConnection() => {|SES1203:"Server=db;Database=app;User Id=sa"|};
            }
            """);

    /// <summary>Verifies the analyzer stays silent when the connection string carries a real password.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task LeavesPopulatedPasswordAloneAsync()
        => await VerifyAsync(
            """
            public class C
            {
                public string GetConnection() => "Server=db;Database=app;User Id=sa;Password=Str0ngP4ss";
            }
            """);

    /// <summary>Verifies the analyzer stays silent when the connection string uses integrated authentication.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task LeavesIntegratedSecurityAloneAsync()
        => await VerifyAsync(
            """
            public class C
            {
                public string GetConnection() => "Server=db;Database=app;User Id=sa;Integrated Security=true";
            }
            """);

    /// <summary>Verifies the analyzer stays silent on an ordinary string literal.</summary>
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
        var test = new AnalyzeEmptyPassword.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = source
        };

        await test.RunAsync(CancellationToken.None);
    }
}
