// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Testing;

using AnalyzeMode = SecuritySharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    SecuritySharp.Analyzers.Ses1308OverPermissiveUnixFileModeAnalyzer>;

namespace SecuritySharp.Analyzers.Tests;

/// <summary>Unit tests for SES1308 (a file or directory must not be created group- or world-writable).</summary>
public class OverPermissiveUnixFileModeAnalyzerUnitTest
{
    /// <summary>Verifies a single <c>OtherWrite</c> member passed to <c>File.SetUnixFileMode</c> is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SingleOtherWriteMemberToSetUnixFileModeReportedAsync()
        => await VerifyNet90Async(
            """
            using System.IO;

            public class C
            {
                public void M(string path)
                {
                    File.SetUnixFileMode(path, {|SES1308:UnixFileMode.OtherWrite|});
                }
            }
            """);

    /// <summary>Verifies an OR-combination that folds in <c>GroupWrite</c> is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task GroupWriteComboToSetUnixFileModeReportedAsync()
        => await VerifyNet90Async(
            """
            using System.IO;

            public class C
            {
                public void M(string path)
                {
                    File.SetUnixFileMode(path, {|SES1308:UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupWrite|});
                }
            }
            """);

    /// <summary>Verifies a broad 0o777-style combo constant is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task BroadAllPermissionsComboReportedAsync()
        => await VerifyNet90Async(
            """
            using System.IO;

            public class C
            {
                private const UnixFileMode All =
                    UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute
                    | UnixFileMode.GroupRead | UnixFileMode.GroupWrite | UnixFileMode.GroupExecute
                    | UnixFileMode.OtherRead | UnixFileMode.OtherWrite | UnixFileMode.OtherExecute;

                public void M(string path)
                {
                    File.SetUnixFileMode(path, {|SES1308:All|});
                }
            }
            """);

    /// <summary>Verifies an over-permissive create mode on <c>Directory.CreateDirectory</c> is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task OtherWriteToCreateDirectoryReportedAsync()
        => await VerifyNet90Async(
            """
            using System.IO;

            public class C
            {
                public void M(string path)
                {
                    Directory.CreateDirectory(path, {|SES1308:UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute | UnixFileMode.OtherWrite|});
                }
            }
            """);

    /// <summary>Verifies a named mode argument is still reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NamedModeArgumentReportedAsync()
        => await VerifyNet90Async(
            """
            using System.IO;

            public class C
            {
                public void M(string path)
                {
                    File.SetUnixFileMode(mode: {|SES1308:UnixFileMode.OtherWrite|}, path: path);
                }
            }
            """);

    /// <summary>Verifies an over-permissive <c>FileStreamOptions.UnixCreateMode</c> initializer is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task FileStreamOptionsUnixCreateModeReportedAsync()
        => await VerifyNet90Async(
            """
            using System.IO;

            public class C
            {
                public void M(string path)
                {
                    var options = new FileStreamOptions
                    {
                        Mode = FileMode.Create,
                        Access = FileAccess.Write,
                        UnixCreateMode = {|SES1308:UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.OtherWrite|}
                    };
                    using var stream = new FileStream(path, options);
                }
            }
            """);

    /// <summary>Verifies an over-permissive assignment to <c>FileInfo.UnixFileMode</c> is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task FileInfoUnixFileModeAssignmentReportedAsync()
        => await VerifyNet90Async(
            """
            using System.IO;

            public class C
            {
                public void M(FileInfo info)
                {
                    info.UnixFileMode = {|SES1308:UnixFileMode.GroupWrite|};
                }
            }
            """);

    /// <summary>Verifies an owner-only mode is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task OwnerOnlyModeIsCleanAsync()
        => await VerifyNet90Async(
            """
            using System.IO;

            public class C
            {
                public void M(string path)
                {
                    File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
                }
            }
            """);

    /// <summary>Verifies a group/other read-only mode (no write bit) is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task GroupOtherReadOnlyModeIsCleanAsync()
        => await VerifyNet90Async(
            """
            using System.IO;

            public class C
            {
                public void M(string path)
                {
                    File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupRead | UnixFileMode.OtherRead);
                }
            }
            """);

    /// <summary>Verifies a non-constant mode held in a variable is not reported (local shape only).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NonConstantModeIsCleanAsync()
        => await VerifyNet90Async(
            """
            using System.IO;

            public class C
            {
                public void M(string path, UnixFileMode mode)
                {
                    File.SetUnixFileMode(path, mode);
                }
            }
            """);

    /// <summary>Verifies a single-argument <c>Directory.CreateDirectory</c> (no mode) is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task CreateDirectoryWithoutModeIsCleanAsync()
        => await VerifyNet90Async(
            """
            using System.IO;

            public class C
            {
                public void M(string path)
                {
                    Directory.CreateDirectory(path);
                }
            }
            """);

    /// <summary>Verifies an unrelated method named <c>SetUnixFileMode</c> on another type is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task UnrelatedSetUnixFileModeIsCleanAsync()
        => await VerifyNet90Async(
            """
            using System.IO;

            public sealed class Fake
            {
                public void SetUnixFileMode(string path, UnixFileMode mode)
                {
                }
            }

            public class C
            {
                public void M(string path)
                {
                    var fake = new Fake();
                    fake.SetUnixFileMode(path, UnixFileMode.OtherWrite);
                }
            }
            """);

    /// <summary>Verifies the rule stays silent on a framework without <c>UnixFileMode</c> (net472).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SilentWhenUnixFileModeUnavailableAsync()
    {
        const string Source = """
                              public enum UnixFileMode
                              {
                                  None = 0,
                                  OtherWrite = 2,
                                  GroupWrite = 16
                              }

                              public static class File
                              {
                                  public static void SetUnixFileMode(string path, UnixFileMode mode)
                                  {
                                  }
                              }

                              public class C
                              {
                                  public void M(string path)
                                  {
                                      File.SetUnixFileMode(path, UnixFileMode.OtherWrite);
                                  }
                              }
                              """;

        var test = new AnalyzeMode.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.NetFramework.Net472.Default,
            TestCode = Source
        };

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Runs an analyzer-only verification against the .NET 9 reference assemblies (where <c>UnixFileMode</c> exists).</summary>
    /// <param name="source">The source with diagnostic markup.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task VerifyNet90Async(string source)
    {
        var test = new AnalyzeMode.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = source
        };

        await test.RunAsync(CancellationToken.None);
    }
}
