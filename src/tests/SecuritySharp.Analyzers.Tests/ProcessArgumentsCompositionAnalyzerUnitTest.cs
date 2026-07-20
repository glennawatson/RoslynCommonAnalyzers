// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Testing;

using AnalyzeArguments = SecuritySharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    SecuritySharp.Analyzers.Ses1301ProcessArgumentsCompositionAnalyzer>;

namespace SecuritySharp.Analyzers.Tests;

/// <summary>Unit tests for SES1301 (a process command line must not be composed from non-constant string parts).</summary>
public class ProcessArgumentsCompositionAnalyzerUnitTest
{
    /// <summary>Verifies an interpolated string assigned to <c>ProcessStartInfo.Arguments</c> is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task InterpolatedArgumentsAssignmentReportedAsync()
        => await VerifyNet90Async(
            """
            using System.Diagnostics;

            public class C
            {
                public void M(string userInput)
                {
                    var psi = new ProcessStartInfo("git");
                    psi.Arguments = {|SES1301:$"clone {userInput}"|};
                }
            }
            """);

    /// <summary>Verifies a string concatenation assigned to <c>ProcessStartInfo.Arguments</c> is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ConcatenatedArgumentsAssignmentReportedAsync()
        => await VerifyNet90Async(
            """
            using System.Diagnostics;

            public class C
            {
                public void M(string userInput)
                {
                    var psi = new ProcessStartInfo("git");
                    psi.Arguments = {|SES1301:"clone " + userInput|};
                }
            }
            """);

    /// <summary>Verifies a composition assigned through an object initializer is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ObjectInitializerArgumentsReportedAsync()
        => await VerifyNet90Async(
            """
            using System.Diagnostics;

            public class C
            {
                public void M(string userInput)
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = "git",
                        Arguments = {|SES1301:$"clone {userInput}"|},
                    };
                }
            }
            """);

    /// <summary>Verifies a concatenated arguments string passed to <c>Process.Start</c> is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ConcatenatedProcessStartArgumentReportedAsync()
        => await VerifyNet90Async(
            """
            using System.Diagnostics;

            public class C
            {
                public void M(string userInput)
                {
                    Process.Start("git", {|SES1301:"clone " + userInput|});
                }
            }
            """);

    /// <summary>Verifies an interpolated arguments string passed by name to <c>Process.Start</c> is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NamedProcessStartArgumentReportedAsync()
        => await VerifyNet90Async(
            """
            using System.Diagnostics;

            public class C
            {
                public void M(string userInput)
                {
                    Process.Start(fileName: "git", arguments: {|SES1301:$"clone {userInput}"|});
                }
            }
            """);

    /// <summary>Verifies a fully-constant concatenated <c>Arguments</c> value is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ConstantConcatenationIsCleanAsync()
        => await VerifyNet90Async(
            """
            using System.Diagnostics;

            public class C
            {
                private const string Repo = "https://example.com/repo.git";

                public void M()
                {
                    var psi = new ProcessStartInfo("git");
                    psi.Arguments = "clone " + Repo;
                }
            }
            """);

    /// <summary>Verifies an interpolated string whose only hole is a constant is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ConstantInterpolationIsCleanAsync()
        => await VerifyNet90Async(
            """
            using System.Diagnostics;

            public class C
            {
                private const string Repo = "repo.git";

                public void M()
                {
                    var psi = new ProcessStartInfo("git");
                    psi.Arguments = $"clone {Repo}";
                }
            }
            """);

    /// <summary>Verifies a plain constant literal <c>Arguments</c> value is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ConstantLiteralArgumentsIsCleanAsync()
        => await VerifyNet90Async(
            """
            using System.Diagnostics;

            public class C
            {
                public void M()
                {
                    var psi = new ProcessStartInfo("git");
                    psi.Arguments = "clone --depth 1";
                }
            }
            """);

    /// <summary>Verifies a bare variable assigned to <c>Arguments</c> is not reported (no local composition shape).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task PlainVariableArgumentsIsCleanAsync()
        => await VerifyNet90Async(
            """
            using System.Diagnostics;

            public class C
            {
                public void M(string args)
                {
                    var psi = new ProcessStartInfo("git");
                    psi.Arguments = args;
                }
            }
            """);

    /// <summary>Verifies each value added through <c>ArgumentList</c> is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ArgumentListIsCleanAsync()
        => await VerifyNet90Async(
            """
            using System.Diagnostics;

            public class C
            {
                public void M(string userInput)
                {
                    var psi = new ProcessStartInfo("git");
                    psi.ArgumentList.Add("clone");
                    psi.ArgumentList.Add(userInput);
                }
            }
            """);

    /// <summary>Verifies the collection overload of <c>Process.Start</c> is not reported (its arguments are not a string).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ProcessStartCollectionOverloadIsCleanAsync()
        => await VerifyNet90Async(
            """
            using System.Collections.Generic;
            using System.Diagnostics;

            public class C
            {
                public void M(IEnumerable<string> args)
                {
                    Process.Start("git", args);
                }
            }
            """);

    /// <summary>Verifies a composition assigned to a same-named property on an unrelated type is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task UnrelatedArgumentsPropertyIsCleanAsync()
        => await VerifyNet90Async(
            """
            using System.Diagnostics;

            public sealed class Command
            {
                public string Arguments { get; set; } = "";
            }

            public class C
            {
                public void M(string userInput)
                {
                    var command = new Command();
                    command.Arguments = "clone " + userInput;
                }
            }
            """);

    /// <summary>Verifies the rule stays silent on a framework without <c>ProcessStartInfo.ArgumentList</c> (net472).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SilentWhenArgumentListUnavailableAsync()
    {
        const string Source = """
                              using System.Diagnostics;

                              public class C
                              {
                                  public void M(string userInput)
                                  {
                                      var psi = new ProcessStartInfo("git");
                                      psi.Arguments = "clone " + userInput;
                                      Process.Start("git", "clone " + userInput);
                                  }
                              }
                              """;

        var test = new AnalyzeArguments.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.NetFramework.Net472.Default,
            TestCode = Source
        };

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Runs an analyzer-only verification against the .NET 9 reference assemblies (where <c>ArgumentList</c> exists).</summary>
    /// <param name="source">The source with diagnostic markup.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task VerifyNet90Async(string source)
    {
        var test = new AnalyzeArguments.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = source
        };

        await test.RunAsync(CancellationToken.None);
    }
}
