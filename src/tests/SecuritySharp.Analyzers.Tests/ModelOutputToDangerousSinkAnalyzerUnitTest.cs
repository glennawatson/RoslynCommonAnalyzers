// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Testing;

using AnalyzeModelSink = SecuritySharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    SecuritySharp.Analyzers.Ses1602ModelOutputToDangerousSinkAnalyzer>;

namespace SecuritySharp.Analyzers.Tests;

/// <summary>Unit tests for SES1602 (AI model output must not flow into a process, file, or raw-SQL sink).</summary>
public class ModelOutputToDangerousSinkAnalyzerUnitTest
{
    /// <summary>Inline stubs for the AI SDK and EF Core raw-SQL surface the rule gates on.</summary>
    private const string Stubs = """

        namespace Microsoft.Extensions.AI
        {
            public class ChatMessage
            {
                public string Text => string.Empty;
            }

            public class ChatResponse
            {
                public string Text => string.Empty;
            }
        }

        namespace Microsoft.EntityFrameworkCore
        {
            public class DatabaseFacade
            {
            }

            public class DbSet<T>
            {
            }

            public static class RelationalDatabaseFacadeExtensions
            {
                public static int ExecuteSqlRaw(this DatabaseFacade databaseFacade, string sql, params object[] parameters) => 0;
            }

            public static class RelationalQueryableExtensions
            {
                public static DbSet<T> FromSqlRaw<T>(this DbSet<T> source, string sql, params object[] parameters) => source;
            }
        }
        """;

    /// <summary>Verifies inline model text into <c>Process.Start</c> filename is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task InlineModelTextToProcessStartFileNameReportedAsync()
        => await VerifyAsync("Process.Start({|SES1602:resp.Text|});");

    /// <summary>Verifies inline model text into the <c>Process.Start</c> arguments string is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task InlineModelTextToProcessStartArgumentsReportedAsync()
        => await VerifyAsync("""Process.Start("cmd", {|SES1602:resp.Text|});""");

    /// <summary>Verifies a named <c>fileName:</c> argument carrying model text is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NamedProcessStartArgumentReportedAsync()
        => await VerifyAsync("Process.Start(fileName: {|SES1602:resp.Text|});");

    /// <summary>Verifies a <c>ProcessStartInfo.FileName</c> object-initializer set to model text is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ProcessStartInfoInitializerFileNameReportedAsync()
        => await VerifyAsync("var info = new ProcessStartInfo { FileName = {|SES1602:resp.Text|} };");

    /// <summary>Verifies a <c>ProcessStartInfo.Arguments</c> assignment set to model text is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ProcessStartInfoArgumentsAssignmentReportedAsync()
        => await VerifyAsync("psi.Arguments = {|SES1602:resp.Text|};");

    /// <summary>Verifies inline model text into a <c>System.IO.File</c> path is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task InlineModelTextToFilePathReportedAsync()
        => await VerifyAsync("File.ReadAllText({|SES1602:resp.Text|});");

    /// <summary>Verifies model text into an EF Core <c>ExecuteSqlRaw</c> command is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ModelTextToExecuteSqlRawReportedAsync()
        => await VerifyAsync("db.ExecuteSqlRaw({|SES1602:resp.Text|});");

    /// <summary>Verifies model text into an EF Core <c>FromSqlRaw</c> command is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ModelTextToFromSqlRawReportedAsync()
        => await VerifyAsync("set.FromSqlRaw({|SES1602:resp.Text|});");

    /// <summary>Verifies model text read from a <c>ChatMessage</c> into a sink is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ChatMessageTextToSinkReportedAsync()
        => await VerifyAsync("Process.Start({|SES1602:msg.Text|});");

    /// <summary>Verifies model text held in the immediately-preceding local is reported at the sink.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ImmediatelyPrecedingLocalReportedAsync()
        => await VerifyAsync(
            """
            var command = resp.Text;
            Process.Start({|SES1602:command|});
            """);

    /// <summary>Verifies a local reused after an intervening statement is not reported (no data-flow tracking).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task LocalWithInterveningStatementIsCleanAsync()
        => await VerifyAsync(
            """
            var command = resp.Text;
            System.Console.WriteLine("audit");
            Process.Start(command);
            """);

    /// <summary>Verifies a non-model string flowing into a sink is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NonModelStringIsCleanAsync()
        => await VerifyAsync("Process.Start(plain);");

    /// <summary>Verifies model text into a non-sink method is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ModelTextToNonSinkIsCleanAsync()
        => await VerifyAsync("System.Console.WriteLine(resp.Text);");

    /// <summary>Verifies model text into a non-path <c>File.WriteAllText</c> content argument is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ModelTextInFileContentArgumentIsCleanAsync()
        => await VerifyAsync("""File.WriteAllText("out.txt", resp.Text);""");

    /// <summary>Verifies a look-alike <c>Text</c> property on an unrelated type is not treated as model output.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task LookAlikeTextPropertyIsCleanAsync()
        => await VerifyAsync(
            """
            var other = new NotAModel();
            Process.Start(other.Text);
            """);

    /// <summary>Verifies a named <c>path:</c> argument carrying model text into a file member is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NamedFilePathArgumentReportedAsync()
        => await VerifyAsync("File.ReadAllText(path: {|SES1602:resp.Text|});");

    /// <summary>Verifies a same-named <c>Start</c> method on an unrelated type is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task UnrelatedStartMethodIsCleanAsync()
        => await VerifyAsync("unrelated.Start(resp.Text);");

    /// <summary>Verifies a same-named file method on an unrelated type is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task UnrelatedFileMethodIsCleanAsync()
        => await VerifyAsync("unrelated.Delete(resp.Text);");

    /// <summary>Verifies a same-named raw-SQL method on an unrelated type is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task UnrelatedRawSqlMethodIsCleanAsync()
        => await VerifyAsync("unrelated.ExecuteSqlRaw(resp.Text);");

    /// <summary>Verifies a same-named <c>FileName</c> property on an unrelated type is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task UnrelatedFileNamePropertyIsCleanAsync()
        => await VerifyAsync("unrelated.FileName = resp.Text;");

    /// <summary>Verifies invocations that are not member calls or carry no arguments are ignored.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NonSinkInvocationShapesAreCleanAsync()
        => await VerifyAsync(
            """
            void Noop()
            {
            }

            Noop();
            plain.Trim();
            """);

    /// <summary>Verifies assignments to non-sink targets, and non-model values into sink targets, are not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NonSinkAssignmentShapesAreCleanAsync()
        => await VerifyAsync(
            """
            psi.Arguments = plain;
            var buffer = new string[1];
            buffer[0] = resp.Text;
            """);

    /// <summary>Verifies a local declared in an outer block but used in a nested block is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task LocalUsedInNestedBlockIsCleanAsync()
        => await VerifyAsync(
            """
            var command = resp.Text;
            if (plain.Length > 0)
            {
                Process.Start(command);
            }
            """);

    /// <summary>Verifies the rule stays silent when the AI SDK types are absent from the compilation.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SilentWhenAiSdkAbsentAsync()
    {
        const string Source = """
                              using System.Diagnostics;

                              public class C
                              {
                                  public void M(string data)
                                  {
                                      Process.Start(data);
                                  }
                              }
                              """;

        await RunAsync(Source);
    }

    /// <summary>Wraps a method body with the sink surface and stubs, then runs analyzer-only verification.</summary>
    /// <param name="body">The method-body statements, with any <c>{|SES1602:...|}</c> markup.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task VerifyAsync(string body)
    {
        var source = $$"""
            using System.Diagnostics;
            using System.IO;
            using Microsoft.Extensions.AI;
            using Microsoft.EntityFrameworkCore;

            public sealed class NotAModel
            {
                public string Text => string.Empty;
            }

            public sealed class Unrelated
            {
                public string FileName { get; set; }

                public string Arguments { get; set; }

                public void Start(string value)
                {
                }

                public void Delete(string value)
                {
                }

                public int ExecuteSqlRaw(string value) => 0;
            }

            public class C
            {
                public void M(ChatResponse resp, ChatMessage msg, ProcessStartInfo psi, DatabaseFacade db, DbSet<int> set, Unrelated unrelated, string plain)
                {
                    {{body}}
                }
            }
            """ + Stubs;

        await RunAsync(source);
    }

    /// <summary>Runs an analyzer-only verification against the .NET 9 reference assemblies.</summary>
    /// <param name="source">The source with diagnostic markup.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task RunAsync(string source)
    {
        var test = new AnalyzeModelSink.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = source
        };

        await test.RunAsync(CancellationToken.None);
    }
}
