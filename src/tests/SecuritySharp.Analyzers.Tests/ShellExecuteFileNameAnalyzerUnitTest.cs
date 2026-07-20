// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Testing;

using AnalyzeShellExecute = SecuritySharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    SecuritySharp.Analyzers.Ses1302ShellExecuteFileNameAnalyzer>;

namespace SecuritySharp.Analyzers.Tests;

/// <summary>Unit tests for SES1302 (a shell-executed ProcessStartInfo must not use a non-constant FileName).</summary>
public class ShellExecuteFileNameAnalyzerUnitTest
{
    /// <summary>Verifies a non-constant initializer <c>FileName</c> under shell execution is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task InitializerNonConstantFileNameReportedAsync()
        => await VerifyNet90Async(
            """
            using System.Diagnostics;

            public class C
            {
                public void M(string fileName)
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = {|SES1302:fileName|},
                        UseShellExecute = true
                    };
                }
            }
            """);

    /// <summary>Verifies the report holds when <c>UseShellExecute</c> is written before <c>FileName</c>.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task UseShellExecuteBeforeFileNameReportedAsync()
        => await VerifyNet90Async(
            """
            using System.Diagnostics;

            public class C
            {
                public void M(string fileName)
                {
                    var psi = new ProcessStartInfo
                    {
                        UseShellExecute = true,
                        FileName = {|SES1302:fileName|}
                    };
                }
            }
            """);

    /// <summary>Verifies a non-constant constructor <c>fileName</c> argument under shell execution is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ConstructorNonConstantFileNameReportedAsync()
        => await VerifyNet90Async(
            """
            using System.Diagnostics;

            public class C
            {
                public void M(string fileName)
                {
                    var psi = new ProcessStartInfo({|SES1302:fileName|})
                    {
                        UseShellExecute = true
                    };
                }
            }
            """);

    /// <summary>Verifies a non-constant constructor <c>fileName</c> passed by name is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NamedConstructorFileNameArgumentReportedAsync()
        => await VerifyNet90Async(
            """
            using System.Diagnostics;

            public class C
            {
                public void M(string fileName)
                {
                    var psi = new ProcessStartInfo(arguments: "-a", fileName: {|SES1302:fileName|})
                    {
                        UseShellExecute = true
                    };
                }
            }
            """);

    /// <summary>Verifies a fully qualified type name is still reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task FullyQualifiedTypeNameReportedAsync()
        => await VerifyNet90Async(
            """
            public class C
            {
                public void M(string fileName)
                {
                    var psi = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = {|SES1302:fileName|},
                        UseShellExecute = true
                    };
                }
            }
            """);

    /// <summary>Verifies a non-constant initializer <c>FileName</c> overrides a constant constructor argument and is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task InitializerFileNameOverridesConstructorReportedAsync()
        => await VerifyNet90Async(
            """
            using System.Diagnostics;

            public class C
            {
                public void M(string fileName)
                {
                    var psi = new ProcessStartInfo("cmd.exe")
                    {
                        UseShellExecute = true,
                        FileName = {|SES1302:fileName|}
                    };
                }
            }
            """);

    /// <summary>Verifies an interpolated (non-constant) filename under shell execution is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task InterpolatedFileNameReportedAsync()
        => await VerifyNet90Async(
            """
            using System.Diagnostics;

            public class C
            {
                public void M(string tool)
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = {|SES1302:$"/usr/bin/{tool}"|},
                        UseShellExecute = true
                    };
                }
            }
            """);

    /// <summary>Verifies a constant string <c>FileName</c> under shell execution is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ConstantFileNameIsCleanAsync()
        => await VerifyNet90Async(
            """
            using System.Diagnostics;

            public class C
            {
                public void M()
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = "cmd.exe",
                        UseShellExecute = true
                    };
                }
            }
            """);

    /// <summary>Verifies a constant constructor <c>fileName</c> argument under shell execution is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ConstantConstructorFileNameIsCleanAsync()
        => await VerifyNet90Async(
            """
            using System.Diagnostics;

            public class C
            {
                public void M()
                {
                    var psi = new ProcessStartInfo("cmd.exe")
                    {
                        UseShellExecute = true
                    };
                }
            }
            """);

    /// <summary>Verifies a constant initializer <c>FileName</c> overriding a non-constant constructor argument is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ConstantInitializerFileNameOverridesConstructorIsCleanAsync()
        => await VerifyNet90Async(
            """
            using System.Diagnostics;

            public class C
            {
                public void M(string fileName)
                {
                    var psi = new ProcessStartInfo(fileName)
                    {
                        UseShellExecute = true,
                        FileName = "cmd.exe"
                    };
                }
            }
            """);

    /// <summary>Verifies a non-constant filename with <c>UseShellExecute = false</c> is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task UseShellExecuteFalseIsCleanAsync()
        => await VerifyNet90Async(
            """
            using System.Diagnostics;

            public class C
            {
                public void M(string fileName)
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = fileName,
                        UseShellExecute = false
                    };
                }
            }
            """);

    /// <summary>Verifies a non-constant filename without any <c>UseShellExecute</c> assignment is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NoUseShellExecuteIsCleanAsync()
        => await VerifyNet90Async(
            """
            using System.Diagnostics;

            public class C
            {
                public void M(string fileName)
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = fileName,
                        Arguments = "-a"
                    };
                }
            }
            """);

    /// <summary>Verifies <c>UseShellExecute = true</c> set from a non-literal value is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NonLiteralUseShellExecuteIsCleanAsync()
        => await VerifyNet90Async(
            """
            using System.Diagnostics;

            public class C
            {
                public void M(string fileName, bool shell)
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = fileName,
                        UseShellExecute = shell
                    };
                }
            }
            """);

    /// <summary>Verifies shell execution with no filename set locally is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ShellExecuteWithoutFileNameIsCleanAsync()
        => await VerifyNet90Async(
            """
            using System.Diagnostics;

            public class C
            {
                public void M()
                {
                    var psi = new ProcessStartInfo
                    {
                        UseShellExecute = true,
                        Arguments = "-a"
                    };
                }
            }
            """);

    /// <summary>Verifies a shell-executed <c>ProcessStartInfo</c> with no initializer is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NoInitializerIsCleanAsync()
        => await VerifyNet90Async(
            """
            using System.Diagnostics;

            public class C
            {
                public void M(string fileName)
                {
                    var psi = new ProcessStartInfo(fileName);
                    psi.UseShellExecute = true;
                }
            }
            """);

    /// <summary>Verifies an unrelated type that merely shares the <c>ProcessStartInfo</c> name is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task UnrelatedProcessStartInfoTypeIsCleanAsync()
        => await VerifyNet90Async(
            """
            public sealed class ProcessStartInfo
            {
                public string FileName { get; set; }

                public bool UseShellExecute { get; set; }
            }

            public class C
            {
                public void M(string fileName)
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = fileName,
                        UseShellExecute = true
                    };
                }
            }
            """);

    /// <summary>Verifies the rule stays silent on a framework without <c>ProcessStartInfo</c>.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SilentWhenProcessStartInfoUnavailableAsync()
    {
        // A framework without System.Diagnostics.ProcessStartInfo (netstandard1.2 predates it): the
        // metadata probe returns null, so nothing is registered. The global stub -- whose metadata name
        // is not the fully-qualified one -- lets the source compile without resolving the real type.
        const string Source = """
                              public sealed class ProcessStartInfo
                              {
                                  public ProcessStartInfo(string fileName)
                                  {
                                  }

                                  public string FileName { get; set; }

                                  public bool UseShellExecute { get; set; }
                              }

                              public class C
                              {
                                  public void M(string fileName)
                                  {
                                      var psi = new ProcessStartInfo(fileName)
                                      {
                                          UseShellExecute = true
                                      };
                                  }
                              }
                              """;

        var test = new AnalyzeShellExecute.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.NetStandard.NetStandard12,
            TestCode = Source
        };

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Runs an analyzer-only verification against the .NET 9 reference assemblies (where <c>ProcessStartInfo</c> exists).</summary>
    /// <param name="source">The source with diagnostic markup.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task VerifyNet90Async(string source)
    {
        var test = new AnalyzeShellExecute.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = source
        };

        await test.RunAsync(CancellationToken.None);
    }
}
