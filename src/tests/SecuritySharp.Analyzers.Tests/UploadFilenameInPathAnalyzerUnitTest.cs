// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Testing;

using AnalyzeUpload = SecuritySharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    SecuritySharp.Analyzers.Ses1305UploadFilenameInPathAnalyzer>;

namespace SecuritySharp.Analyzers.Tests;

/// <summary>Unit tests for SES1305 (an uploaded file name must not build a storage path).</summary>
public class UploadFilenameInPathAnalyzerUnitTest
{
    /// <summary>An inline <c>IFormFile</c> marker so the rule activates against the .NET 9 reference set.</summary>
    private const string FormFileStub = """


        namespace Microsoft.AspNetCore.Http
        {
            public interface IFormFile
            {
                string FileName { get; }
            }
        }
        """;

    /// <summary>Verifies a filename passed directly to <c>Path.Combine</c> is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task PathCombineArgumentReportedAsync()
        => await VerifyAsync(
            """
            using System.IO;
            using Microsoft.AspNetCore.Http;

            public class C
            {
                public string M(IFormFile file, string root)
                    => Path.Combine(root, {|SES1305:file.FileName|});
            }
            """);

    /// <summary>Verifies a filename in a <c>+</c> concatenation with a separator literal is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task PathConcatWithSeparatorLiteralReportedAsync()
        => await VerifyAsync(
            """
            using Microsoft.AspNetCore.Http;

            public class C
            {
                public string M(IFormFile file, string root)
                    => root + "/uploads/" + {|SES1305:file.FileName|};
            }
            """);

    /// <summary>Verifies a filename in a <c>+</c> chain whose only separator is a bare literal is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task PathConcatChainReportedAsync()
        => await VerifyAsync(
            """
            using Microsoft.AspNetCore.Http;

            public class C
            {
                public string M(IFormFile file, string root)
                    => root + "/" + {|SES1305:file.FileName|};
            }
            """);

    /// <summary>Verifies a filename passed to <c>File.WriteAllBytes</c> is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task FileWriteAllBytesArgumentReportedAsync()
        => await VerifyAsync(
            """
            using System.IO;
            using Microsoft.AspNetCore.Http;

            public class C
            {
                public void M(IFormFile file, byte[] bytes)
                    => File.WriteAllBytes({|SES1305:file.FileName|}, bytes);
            }
            """);

    /// <summary>Verifies a filename passed to <c>File.Create</c> is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task FileCreateArgumentReportedAsync()
        => await VerifyAsync(
            """
            using System.IO;
            using Microsoft.AspNetCore.Http;

            public class C
            {
                public void M(IFormFile file)
                {
                    using var stream = File.Create({|SES1305:file.FileName|});
                }
            }
            """);

    /// <summary>Verifies a filename passed to <c>File.OpenWrite</c> is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task FileOpenWriteArgumentReportedAsync()
        => await VerifyAsync(
            """
            using System.IO;
            using Microsoft.AspNetCore.Http;

            public class C
            {
                public void M(IFormFile file)
                {
                    using var stream = File.OpenWrite({|SES1305:file.FileName|});
                }
            }
            """);

    /// <summary>Verifies a filename passed as the destination of <c>File.Copy</c> is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task FileCopyDestinationArgumentReportedAsync()
        => await VerifyAsync(
            """
            using System.IO;
            using Microsoft.AspNetCore.Http;

            public class C
            {
                public void M(IFormFile file, string source)
                    => File.Copy(source, {|SES1305:file.FileName|});
            }
            """);

    /// <summary>Verifies a filename passed to a <c>new FileStream(...)</c> constructor is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task FileStreamConstructorArgumentReportedAsync()
        => await VerifyAsync(
            """
            using System.IO;
            using Microsoft.AspNetCore.Http;

            public class C
            {
                public void M(IFormFile file)
                {
                    using var stream = new FileStream({|SES1305:file.FileName|}, FileMode.Create);
                }
            }
            """);

    /// <summary>Verifies the rule fires through fully-qualified sink names.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task FullyQualifiedFileStreamConstructorReportedAsync()
        => await VerifyAsync(
            """
            using Microsoft.AspNetCore.Http;

            public class C
            {
                public void M(IFormFile file)
                {
                    using var stream = new System.IO.FileStream({|SES1305:file.FileName|}, System.IO.FileMode.Create);
                }
            }
            """);

    /// <summary>Verifies a filename sanitized with <c>Path.GetFileName</c> before combining is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SanitizedWithGetFileNameIsCleanAsync()
        => await VerifyAsync(
            """
            using System.IO;
            using Microsoft.AspNetCore.Http;

            public class C
            {
                public string M(IFormFile file, string root)
                    => Path.Combine(root, Path.GetFileName(file.FileName));
            }
            """);

    /// <summary>Verifies a concatenation without any path-separator literal is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NonPathConcatenationIsCleanAsync()
        => await VerifyAsync(
            """
            using Microsoft.AspNetCore.Http;

            public class C
            {
                public string M(IFormFile file)
                    => "Uploaded file: " + file.FileName;
            }
            """);

    /// <summary>Verifies a same-named <c>FileName</c> on a non-upload type is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SameNamedFileNameOnOtherTypeIsCleanAsync()
        => await VerifyAsync(
            """
            using System.IO;

            public sealed class NotAnUpload
            {
                public string FileName => "report.txt";
            }

            public class C
            {
                public string M(NotAnUpload file, string root)
                    => Path.Combine(root, file.FileName);
            }
            """);

    /// <summary>Verifies a filename read into a call that is not a path sink is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task UnrelatedCallArgumentIsCleanAsync()
        => await VerifyAsync(
            """
            using Microsoft.AspNetCore.Http;

            public class C
            {
                public void M(IFormFile file)
                    => System.Console.WriteLine(file.FileName);
            }
            """);

    /// <summary>Verifies a filename passed to a file-reading call (not a creating call) is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task FileReadCallIsCleanAsync()
        => await VerifyAsync(
            """
            using System.IO;
            using Microsoft.AspNetCore.Http;

            public class C
            {
                public string M(IFormFile file)
                    => File.ReadAllText(file.FileName);
            }
            """);

    /// <summary>Verifies a filename merely stored in a local (no path sink) is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task PlainLocalAssignmentIsCleanAsync()
        => await VerifyAsync(
            """
            using Microsoft.AspNetCore.Http;

            public class C
            {
                public string M(IFormFile file)
                {
                    var name = file.FileName;
                    return name;
                }
            }
            """);

    /// <summary>Verifies the rule stays silent when the <c>IFormFile</c> marker type is absent.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SilentWhenFormFileUnavailableAsync()
    {
        const string Source = """
                              using System.IO;
                              using Other;

                              public class C
                              {
                                  public string M(IFormFile file, string root)
                                      => Path.Combine(root, file.FileName);
                              }

                              namespace Other
                              {
                                  public interface IFormFile
                                  {
                                      string FileName { get; }
                                  }
                              }
                              """;

        var test = new AnalyzeUpload.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = Source
        };

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Runs an analyzer-only verification against the .NET 9 reference set with the <c>IFormFile</c> marker in scope.</summary>
    /// <param name="source">The source with diagnostic markup.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task VerifyAsync(string source)
    {
        var test = new AnalyzeUpload.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = source + FormFileStub
        };

        await test.RunAsync(CancellationToken.None);
    }
}
