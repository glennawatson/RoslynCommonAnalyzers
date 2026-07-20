// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Testing;

using AnalyzeTempFile = SecuritySharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    SecuritySharp.Analyzers.Ses1307InsecureTempFileAnalyzer>;

namespace SecuritySharp.Analyzers.Tests;

/// <summary>Unit tests for SES1307 (do not create predictable temporary files with Path.GetTempFileName).</summary>
public class InsecureTempFileAnalyzerUnitTest
{
    /// <summary>Verifies a member-access <c>Path.GetTempFileName()</c> call is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task MemberAccessCallReportedAsync()
        => await VerifyNet90Async(
            """
            using System.IO;

            public class C
            {
                public string M() => {|SES1307:Path.GetTempFileName()|};
            }
            """);

    /// <summary>Verifies a fully-qualified <c>System.IO.Path.GetTempFileName()</c> call is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task FullyQualifiedCallReportedAsync()
        => await VerifyNet90Async(
            """
            public class C
            {
                public string M() => {|SES1307:System.IO.Path.GetTempFileName()|};
            }
            """);

    /// <summary>Verifies a <c>using static</c> bare <c>GetTempFileName()</c> call is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task UsingStaticCallReportedAsync()
        => await VerifyNet90Async(
            """
            using static System.IO.Path;

            public class C
            {
                public string M() => {|SES1307:GetTempFileName()|};
            }
            """);

    /// <summary>Verifies the rule still fires on a framework without the isolated-directory replacement (net472).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ReportedOnNetFrameworkAsync()
    {
        const string Source = """
                              using System.IO;

                              public class C
                              {
                                  public string M() => {|SES1307:Path.GetTempFileName()|};
                              }
                              """;

        var test = new AnalyzeTempFile.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.NetFramework.Net472.Default,
            TestCode = Source
        };

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies a same-named method on an unrelated type is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SameNamedMethodOnOtherTypeIsCleanAsync()
        => await VerifyNet90Async(
            """
            public static class MyIo
            {
                public static string GetTempFileName() => "safe";
            }

            public class C
            {
                public string M() => MyIo.GetTempFileName();
            }
            """);

    /// <summary>Verifies an unrelated <c>Path</c> method is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task OtherPathMethodIsCleanAsync()
        => await VerifyNet90Async(
            """
            using System.IO;

            public class C
            {
                public string M() => Path.GetRandomFileName();

                public string N() => Path.GetTempPath();
            }
            """);

    /// <summary>Verifies an instance method named <c>GetTempFileName</c> is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task InstanceMethodIsCleanAsync()
        => await VerifyNet90Async(
            """
            public class Helper
            {
                public string GetTempFileName() => "safe";
            }

            public class C
            {
                public string M(Helper helper) => helper.GetTempFileName();
            }
            """);

    /// <summary>Runs an analyzer-only verification against the .NET 9 reference assemblies.</summary>
    /// <param name="source">The source with diagnostic markup.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task VerifyNet90Async(string source)
    {
        var test = new AnalyzeTempFile.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = source
        };

        await test.RunAsync(CancellationToken.None);
    }
}
