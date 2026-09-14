// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task PathCombineArgumentReportedAsync() =>
        VerifyAsync(
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task PathConcatWithSeparatorLiteralReportedAsync() =>
        VerifyAsync(
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task PathConcatChainReportedAsync() =>
        VerifyAsync(
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task FileWriteAllBytesArgumentReportedAsync() =>
        VerifyAsync(
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task FileCreateArgumentReportedAsync() =>
        VerifyAsync(
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task FileOpenWriteArgumentReportedAsync() =>
        VerifyAsync(
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task FileCopyDestinationArgumentReportedAsync() =>
        VerifyAsync(
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task FileStreamConstructorArgumentReportedAsync() =>
        VerifyAsync(
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task FullyQualifiedFileStreamConstructorReportedAsync() =>
        VerifyAsync(
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task SanitizedWithGetFileNameIsCleanAsync() =>
        VerifyAsync(
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task NonPathConcatenationIsCleanAsync() =>
        VerifyAsync(
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task SameNamedFileNameOnOtherTypeIsCleanAsync() =>
        VerifyAsync(
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task UnrelatedCallArgumentIsCleanAsync() =>
        VerifyAsync(
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task FileReadCallIsCleanAsync() =>
        VerifyAsync(
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task PlainLocalAssignmentIsCleanAsync() =>
        VerifyAsync(
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

    /// <summary>Verifies parentheses and nested additive chains retain path separators.</summary>
    /// <param name="expression">The path expression with its unsafe filename marked.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    [Arguments("((({|SES1305:file.FileName|}))) + \"/\"")]
    [Arguments("({|SES1305:file.FileName|} + \"suffix\") + \"/\"")]
    [Arguments("\"root\" + (({|SES1305:file.FileName|}) + (\"/\"))")]
    [Arguments("\"root\\\\\" + {|SES1305:file.FileName|}")]
    [Arguments("(({|SES1305:file.FileName|} + \"suffix\")) + (\"tail\" + \"/\")")]
    public Task ParenthesizedPathConcatenationIsReportedAsync(string expression) =>
        VerifyAsync($$"""
            using Microsoft.AspNetCore.Http;
            class C { string M(IFormFile file) => {{expression}}; }
            """);

    /// <summary>Verifies expressions outside the supported direct-argument and literal-separator shapes are silent.</summary>
    /// <param name="expression">The expression whose filename is not a supported sink operand.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    [Arguments("file.FileName == \"/\"")]
    [Arguments("(file.FileName + \"suffix\") == \"name\"")]
    [Arguments("file.FileName + string.Empty")]
    [Arguments("file.FileName + (1 + 2)")]
    [Arguments("file.FileName + '/' ")]
    [Arguments("System.IO.Path.Combine(\"root\", (file.FileName))")]
    [Arguments("new System.IO.FileInfo(file.FileName)")]
    [Arguments("new FileStream<string>(file.FileName)")]
    [Arguments("new Alias::FileStream(file.FileName)")]
    [Arguments("new System.IO.FileStream[] { }[file.FileName.Length]")]
    public Task UnsupportedPathShapeIsSilentAsync(string expression) =>
        VerifyAsync($$"""
            using Alias = Probe;
            using Microsoft.AspNetCore.Http;
            class C { object M(IFormFile file) => {{expression}}; }
            class FileStream<T> { public FileStream(string name) { } }
            namespace Probe { class FileStream { public FileStream(string name) { } } }
            """);

    /// <summary>Verifies an indexer argument is not treated as an invocation argument.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task FilenameIndexerArgumentIsSilentAsync() =>
        VerifyAsync("""
            using Microsoft.AspNetCore.Http;
            class C
            {
                string M(IFormFile file, System.Collections.Generic.Dictionary<string, string> names) => names[file.FileName];
            }
            """);

    /// <summary>Verifies a base-constructor argument is not a supported filesystem sink.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task FilenameBaseConstructorArgumentIsSilentAsync() =>
        VerifyAsync("""
            using Microsoft.AspNetCore.Http;
            class Base { public Base(string name) { } }
            class C : Base { public C(IFormFile file) : base(file.FileName) { } }
            """);

    /// <summary>Verifies sink-like method and constructor names on unrelated types are rejected after binding.</summary>
    /// <param name="expression">The unrelated call that accepts the filename.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    [Arguments("Combine(file.FileName)")]
    [Arguments("Create(file.FileName)")]
    [Arguments("new FileStream(file.FileName)")]
    public Task SameNamedSinkOnOtherTypeIsSilentAsync(string expression) =>
        VerifyAsync($$"""
            using Microsoft.AspNetCore.Http;
            class C
            {
                object M(IFormFile file) => {{expression}};
                static string Combine(string name) => name;
                static string Create(string name) => name;
            }
            class FileStream { public FileStream(string name) { } }
            """);

    /// <summary>Verifies an upload-looking field and a concrete implementation property are not the interface property.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task FilenameMustBindToTheInterfacePropertyAsync() =>
        VerifyAsync("""
            using System.IO;
            using Microsoft.AspNetCore.Http;
            class FieldUpload { public string FileName; }
            class Upload : IFormFile { public string FileName => "name"; }
            class C
            {
                string Field(FieldUpload file) => Path.Combine("root", file.FileName);
                string Concrete(Upload file) => Path.Combine("root", file.FileName);
                string Interface(Upload file) => Path.Combine("root", {|SES1305:((IFormFile)file).FileName|});
            }
            """);

    /// <summary>Verifies unresolved upload members and sink overloads cannot establish an unsafe call.</summary>
    /// <param name="expression">The incomplete path expression.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    [Arguments("System.IO.Path.Combine(\"root\", missing.FileName)")]
    [Arguments("System.IO.Path.Combine(file.FileName, 1)")]
    public Task UnresolvedPathCallIsSilentAsync(string expression) =>
        new AnalyzeUpload.Test
        {
            ReferenceAssemblies = RoslynCommon.Analyzers.Tests.AnalyzerFrameworks.Net90,
            TestCode = $$"""
                using Microsoft.AspNetCore.Http;
                class C { object M(IFormFile file) => {{expression}}; }
                """ + FormFileStub,
            CompilerDiagnostics = CompilerDiagnostics.None,
        }.RunAsync(CancellationToken.None);

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

        var test = new AnalyzeUpload.Test { ReferenceAssemblies = ReferenceAssemblies.Net.Net90, TestCode = Source };

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Runs an analyzer-only verification against the .NET 9 reference set with the <c>IFormFile</c> marker in scope.</summary>
    /// <param name="source">The source with diagnostic markup.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task VerifyAsync(string source)
    {
        var test = new AnalyzeUpload.Test { ReferenceAssemblies = ReferenceAssemblies.Net.Net90, TestCode = source + FormFileStub };

        await test.RunAsync(CancellationToken.None);
    }
}
