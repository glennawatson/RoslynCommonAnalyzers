// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Testing;

using AnalyzeTool = SecuritySharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    SecuritySharp.Analyzers.Ses1603NonDestructiveToolMutationAnalyzer>;

namespace SecuritySharp.Analyzers.Tests;

/// <summary>Unit tests for SES1603 (a read-only/non-destructive AI tool must not call a destructive API).</summary>
public class NonDestructiveToolMutationAnalyzerUnitTest
{
    /// <summary>The model-tool attribute stub, mirroring the real settable read-only/destructive hints.</summary>
    private const string McpStub = """


        namespace ModelContextProtocol.Server
        {
            using System;

            [AttributeUsage(AttributeTargets.Method)]
            public sealed class McpServerToolAttribute : Attribute
            {
                public string Name { get; set; }

                public bool ReadOnly { get; set; }

                public bool Destructive { get; set; }
            }
        }
        """;

    /// <summary>The Entity Framework stub appended alongside <see cref="McpStub"/> for the database-sink samples.</summary>
    private const string EntityFrameworkStub = """


        namespace Microsoft.EntityFrameworkCore
        {
            public class DbContext
            {
                public virtual int SaveChanges() => 0;
            }

            public static class RelationalDatabaseFacadeExtensions
            {
                public static int ExecuteSqlRaw(this DbContext context, string sql) => 0;
            }
        }
        """;

    /// <summary>Verifies a read-only tool that deletes a file is reported at the delete call.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ReadOnlyToolDeletingFileReportedAsync()
        => await VerifyToolAsync(
            """
            using System.IO;
            using ModelContextProtocol.Server;

            public class Tools
            {
                [McpServerTool(ReadOnly = true)]
                public void Cleanup(string path) => {|SES1603:File.Delete(path)|};
            }
            """);

    /// <summary>Verifies a non-destructive tool that deletes a directory is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NonDestructiveToolDeletingDirectoryReportedAsync()
        => await VerifyToolAsync(
            """
            using System.IO;
            using ModelContextProtocol.Server;

            public class Tools
            {
                [McpServerTool(Destructive = false)]
                public void Purge(string path)
                {
                    {|SES1603:Directory.Delete(path, true)|};
                }
            }
            """);

    /// <summary>Verifies a read-only tool that overwrites a file is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ReadOnlyToolOverwritingFileReportedAsync()
        => await VerifyToolAsync(
            """
            using System.IO;
            using ModelContextProtocol.Server;

            public class Tools
            {
                [McpServerTool(ReadOnly = true)]
                public void Save(string path) => {|SES1603:File.WriteAllText(path, "data")|};
            }
            """);

    /// <summary>Verifies a read-only tool that starts a process is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ReadOnlyToolStartingProcessReportedAsync()
        => await VerifyToolAsync(
            """
            using System.Diagnostics;
            using ModelContextProtocol.Server;

            public class Tools
            {
                [McpServerTool(ReadOnly = true)]
                public void Run(string tool) => {|SES1603:Process.Start(tool)|};
            }
            """);

    /// <summary>Verifies a read-only tool that runs a non-query database command is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ReadOnlyToolExecutingNonQueryReportedAsync()
        => await VerifyToolAsync(
            """
            using System.Data.Common;
            using ModelContextProtocol.Server;

            public class Tools
            {
                [McpServerTool(ReadOnly = true)]
                public void Wipe(DbCommand command) => {|SES1603:command.ExecuteNonQuery()|};
            }
            """);

    /// <summary>Verifies a read-only tool that persists Entity Framework changes is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ReadOnlyToolSavingChangesReportedAsync()
        => await VerifyToolAsync(
            """
            using Microsoft.EntityFrameworkCore;
            using ModelContextProtocol.Server;

            public class Tools
            {
                [McpServerTool(ReadOnly = true)]
                public void Commit(DbContext context) => {|SES1603:context.SaveChanges()|};
            }
            """);

    /// <summary>Verifies a non-destructive tool that runs raw SQL is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NonDestructiveToolExecutingRawSqlReportedAsync()
        => await VerifyToolAsync(
            """
            using Microsoft.EntityFrameworkCore;
            using ModelContextProtocol.Server;

            public class Tools
            {
                [McpServerTool(Destructive = false)]
                public void Truncate(DbContext context) => {|SES1603:context.ExecuteSqlRaw("DELETE FROM t")|};
            }
            """);

    /// <summary>Verifies a save on a custom <c>DbContext</c> subclass that overrides <c>SaveChanges</c> is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ReadOnlyToolSavingChangesOnDerivedContextReportedAsync()
        => await VerifyToolAsync(
            """
            using Microsoft.EntityFrameworkCore;
            using ModelContextProtocol.Server;

            public class BlogContext : DbContext
            {
                public override int SaveChanges() => base.SaveChanges();
            }

            public class Tools
            {
                [McpServerTool(ReadOnly = true)]
                public void Commit(BlogContext context) => {|SES1603:context.SaveChanges()|};
            }
            """);

    /// <summary>Verifies a tool with no read-only/non-destructive hint is not reported (unset defaults to destructive).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ToolWithoutHintIsCleanAsync()
        => await VerifyToolAsync(
            """
            using System.IO;
            using ModelContextProtocol.Server;

            public class Tools
            {
                [McpServerTool]
                public void Cleanup(string path) => File.Delete(path);
            }
            """);

    /// <summary>Verifies an explicit <c>ReadOnly = false</c> tool is not reported (it makes no safety promise).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ExplicitReadOnlyFalseToolIsCleanAsync()
        => await VerifyToolAsync(
            """
            using System.IO;
            using ModelContextProtocol.Server;

            public class Tools
            {
                [McpServerTool(ReadOnly = false)]
                public void Cleanup(string path) => File.Delete(path);
            }
            """);

    /// <summary>Verifies an explicit <c>Destructive = true</c> tool is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ExplicitDestructiveTrueToolIsCleanAsync()
        => await VerifyToolAsync(
            """
            using System.IO;
            using ModelContextProtocol.Server;

            public class Tools
            {
                [McpServerTool(Destructive = true)]
                public void Cleanup(string path) => File.Delete(path);
            }
            """);

    /// <summary>Verifies a read-only tool that only reads a file is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ReadOnlyToolReadingFileIsCleanAsync()
        => await VerifyToolAsync(
            """
            using System.IO;
            using ModelContextProtocol.Server;

            public class Tools
            {
                [McpServerTool(ReadOnly = true)]
                public string Read(string path) => File.ReadAllText(path);
            }
            """);

    /// <summary>Verifies a plain method that is not a tool is not reported even when it deletes a file.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NonToolMethodIsCleanAsync()
        => await VerifyToolAsync(
            """
            using System.IO;

            public class Tools
            {
                public void Cleanup(string path) => File.Delete(path);
            }
            """);

    /// <summary>Verifies a read-only hint alongside an unrelated named argument is still honoured.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ReadOnlyToolWithNameArgumentReportedAsync()
        => await VerifyToolAsync(
            """
            using System.IO;
            using ModelContextProtocol.Server;

            public class Tools
            {
                [McpServerTool(Name = "cleanup", ReadOnly = true)]
                public void Cleanup(string path) => {|SES1603:File.Delete(path)|};
            }
            """);

    /// <summary>Verifies an abstract tool declaration with no body is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task AbstractToolMethodIsCleanAsync()
        => await VerifyToolAsync(
            """
            using ModelContextProtocol.Server;

            public abstract class Tools
            {
                [McpServerTool(ReadOnly = true)]
                public abstract void Cleanup(string path);
            }
            """);

    /// <summary>Verifies a read-only tool that only reads via a command is clean when Entity Framework is absent.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ReadOnlyToolReadingViaCommandWithoutEntityFrameworkIsCleanAsync()
        => await VerifyToolWithoutEntityFrameworkAsync(
            """
            using System.Data.Common;
            using ModelContextProtocol.Server;

            public class Tools
            {
                [McpServerTool(ReadOnly = true)]
                public void Peek(DbCommand command) => command.ExecuteReader();
            }
            """);

    /// <summary>Verifies a Semantic Kernel <c>[KernelFunction]</c> method is not covered (it carries no safety hint).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task KernelFunctionMethodIsCleanAsync()
        => await VerifyToolAsync(
            """
            using System;
            using System.IO;

            namespace Microsoft.SemanticKernel
            {
                [AttributeUsage(AttributeTargets.Method)]
                public sealed class KernelFunctionAttribute : Attribute
                {
                }
            }

            public class Tools
            {
                [Microsoft.SemanticKernel.KernelFunction]
                public void Cleanup(string path) => File.Delete(path);
            }
            """);

    /// <summary>Verifies the rule stays silent when the model-tool attribute type is not present.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SilentWhenToolAttributeUnavailableAsync()
        => await VerifyRawAsync(
            """
            using System;
            using System.IO;

            [AttributeUsage(AttributeTargets.Method)]
            public sealed class ToolAttribute : Attribute
            {
                public bool ReadOnly { get; set; }
            }

            public class Tools
            {
                [Tool(ReadOnly = true)]
                public void Cleanup(string path) => File.Delete(path);
            }
            """);

    /// <summary>Runs an analyzer-only verification of a tool sample against the .NET 9 reference assemblies.</summary>
    /// <param name="toolSource">The tool source with diagnostic markup; the model-tool and EF stubs are appended.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task VerifyToolAsync(string toolSource) => await VerifyRawAsync(toolSource + McpStub + EntityFrameworkStub);

    /// <summary>Runs an analyzer-only verification of a tool sample compiled without the Entity Framework stub.</summary>
    /// <param name="toolSource">The tool source with diagnostic markup; only the model-tool stub is appended.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task VerifyToolWithoutEntityFrameworkAsync(string toolSource) => await VerifyRawAsync(toolSource + McpStub);

    /// <summary>Runs an analyzer-only verification of a complete source against the .NET 9 reference assemblies.</summary>
    /// <param name="source">The complete source with diagnostic markup.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task VerifyRawAsync(string source)
    {
        var test = new AnalyzeTool.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = source
        };

        await test.RunAsync(CancellationToken.None);
    }
}
