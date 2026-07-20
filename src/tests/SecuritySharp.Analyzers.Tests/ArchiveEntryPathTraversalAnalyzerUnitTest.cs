// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Testing;

using AnalyzePathTraversal = SecuritySharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    SecuritySharp.Analyzers.Ses1304ArchiveEntryPathTraversalAnalyzer>;

namespace SecuritySharp.Analyzers.Tests;

/// <summary>Unit tests for SES1304 (an archive entry name must not build a write path without a containment check).</summary>
public class ArchiveEntryPathTraversalAnalyzerUnitTest
{
    /// <summary>Verifies a zip entry name joined into <c>File.WriteAllBytes</c> is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ZipEntryIntoWriteAllBytesReportedAsync()
        => await VerifyNet90Async(
            """
            using System.IO;
            using System.IO.Compression;

            public class C
            {
                public void M(ZipArchiveEntry entry, string destDir, byte[] bytes)
                {
                    File.WriteAllBytes({|SES1304:Path.Combine(destDir, entry.FullName)|}, bytes);
                }
            }
            """);

    /// <summary>Verifies a zip entry name joined into <c>File.Create</c> is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ZipEntryIntoFileCreateReportedAsync()
        => await VerifyNet90Async(
            """
            using System.IO;
            using System.IO.Compression;

            public class C
            {
                public void M(ZipArchiveEntry entry, string destDir)
                {
                    using var stream = File.Create({|SES1304:Path.Combine(destDir, entry.FullName)|});
                }
            }
            """);

    /// <summary>Verifies a zip entry name concatenated into <c>File.WriteAllText</c> is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ZipEntryConcatIntoWriteAllTextReportedAsync()
        => await VerifyNet90Async(
            """
            using System.IO;
            using System.IO.Compression;

            public class C
            {
                public void M(ZipArchiveEntry entry, string destDir, string text)
                {
                    File.WriteAllText({|SES1304:destDir + entry.FullName|}, text);
                }
            }
            """);

    /// <summary>Verifies a zip entry name passed by an explicit <c>path:</c> name is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ZipEntryNamedPathArgumentReportedAsync()
        => await VerifyNet90Async(
            """
            using System.IO;
            using System.IO.Compression;

            public class C
            {
                public void M(ZipArchiveEntry entry, string destDir, byte[] bytes)
                {
                    File.WriteAllBytes(bytes: bytes, path: {|SES1304:Path.Combine(destDir, entry.FullName)|});
                }
            }
            """);

    /// <summary>Verifies a Tar entry name joined into <c>TarEntry.ExtractToFile</c> is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task TarEntryIntoExtractToFileReportedAsync()
        => await VerifyNet90Async(
            """
            using System.IO;
            using System.Formats.Tar;

            public class C
            {
                public void M(TarEntry entry, string destDir)
                {
                    entry.ExtractToFile({|SES1304:Path.Combine(destDir, entry.Name)|}, true);
                }
            }
            """);

    /// <summary>Verifies a Tar entry name joined into a <c>new FileStream</c> is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task TarEntryIntoFileStreamReportedAsync()
        => await VerifyNet90Async(
            """
            using System.IO;
            using System.Formats.Tar;

            public class C
            {
                public void M(TarEntry entry, string destDir)
                {
                    using var stream = new FileStream({|SES1304:Path.Combine(destDir, entry.Name)|}, FileMode.Create);
                }
            }
            """);

    /// <summary>Verifies a Tar entry name joined into <c>File.OpenWrite</c> is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task TarEntryIntoOpenWriteReportedAsync()
        => await VerifyNet90Async(
            """
            using System.IO;
            using System.Formats.Tar;

            public class C
            {
                public void M(TarEntry entry, string destDir)
                {
                    using var stream = File.OpenWrite({|SES1304:Path.Combine(destDir, entry.Name)|});
                }
            }
            """);

    /// <summary>Verifies a Tar entry name concatenated into <c>File.WriteAllBytes</c> is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task TarEntryConcatIntoWriteAllBytesReportedAsync()
        => await VerifyNet90Async(
            """
            using System.IO;
            using System.Formats.Tar;

            public class C
            {
                public void M(TarEntry entry, string destDir, byte[] bytes)
                {
                    File.WriteAllBytes({|SES1304:destDir + entry.Name|}, bytes);
                }
            }
            """);

    /// <summary>Verifies the zip <c>ExtractToFile</c> sink is not reported (covered by the built-in archive analysis).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ZipEntryIntoExtractToFileIsCleanAsync()
        => await VerifyNet90Async(
            """
            using System.IO;
            using System.IO.Compression;

            public class C
            {
                public void M(ZipArchiveEntry entry, string destDir)
                {
                    entry.ExtractToFile(Path.Combine(destDir, entry.FullName), true);
                }
            }
            """);

    /// <summary>Verifies the zip <c>FileStream</c> sink is not reported (covered by the built-in archive analysis).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ZipEntryIntoFileStreamIsCleanAsync()
        => await VerifyNet90Async(
            """
            using System.IO;
            using System.IO.Compression;

            public class C
            {
                public void M(ZipArchiveEntry entry, string destDir)
                {
                    using var stream = new FileStream(Path.Combine(destDir, entry.FullName), FileMode.Create);
                }
            }
            """);

    /// <summary>Verifies the zip <c>File.OpenWrite</c> sink is not reported (covered by the built-in archive analysis).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ZipEntryIntoOpenWriteIsCleanAsync()
        => await VerifyNet90Async(
            """
            using System.IO;
            using System.IO.Compression;

            public class C
            {
                public void M(ZipArchiveEntry entry, string destDir)
                {
                    using var stream = File.OpenWrite(Path.Combine(destDir, entry.FullName));
                }
            }
            """);

    /// <summary>Verifies the multi-statement form (entry name copied into a local) is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task EntryNameThroughLocalIsCleanAsync()
        => await VerifyNet90Async(
            """
            using System.IO;
            using System.IO.Compression;

            public class C
            {
                public void M(ZipArchiveEntry entry, string destDir, byte[] bytes)
                {
                    string name = entry.FullName;
                    File.WriteAllBytes(Path.Combine(destDir, name), bytes);
                }
            }
            """);

    /// <summary>Verifies a destination joining a constant (not an entry name) is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ConstantDestinationIsCleanAsync()
        => await VerifyNet90Async(
            """
            using System.IO;
            using System.IO.Compression;

            public class C
            {
                public void M(ZipArchiveEntry entry, string destDir, byte[] bytes)
                {
                    File.WriteAllBytes(Path.Combine(destDir, "fixed.dat"), bytes);
                }
            }
            """);

    /// <summary>Verifies an entry name wrapped in a sanitizing call (not a direct join argument) is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SanitizedEntryNameIsCleanAsync()
        => await VerifyNet90Async(
            """
            using System.IO;
            using System.IO.Compression;

            public class C
            {
                public void M(ZipArchiveEntry entry, string destDir, byte[] bytes)
                {
                    File.WriteAllBytes(Path.Combine(destDir, Sanitize(entry.FullName)), bytes);
                }

                private static string Sanitize(string name) => name.Replace("..", string.Empty);
            }
            """);

    /// <summary>Verifies a same-named <c>.Name</c> on an unrelated type (not an archive entry) is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NonArchiveNamePropertyIsCleanAsync()
        => await VerifyNet90Async(
            """
            using System.IO;

            public class C
            {
                public void M(DirectoryInfo dir, string destDir, byte[] bytes)
                {
                    File.WriteAllBytes(Path.Combine(destDir, dir.Name), bytes);
                }
            }
            """);

    /// <summary>Verifies a look-alike <c>ZipArchiveEntry</c> from another namespace is not reported (the type is bound, not name-matched).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task LookAlikeArchiveEntryIsCleanAsync()
        => await VerifyNet90Async(
            """
            using System.IO;

            namespace Other
            {
                public sealed class ZipArchiveEntry
                {
                    public string FullName => string.Empty;
                }

                public class C
                {
                    public void M(ZipArchiveEntry entry, string destDir, byte[] bytes)
                    {
                        File.WriteAllBytes(Path.Combine(destDir, entry.FullName), bytes);
                    }
                }
            }
            """);

    /// <summary>Verifies a Tar entry name on the left of a concatenation is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task TarEntryLeftConcatReportedAsync()
        => await VerifyNet90Async(
            """
            using System.IO;
            using System.Formats.Tar;

            public class C
            {
                public void M(TarEntry entry, string destDir, byte[] bytes)
                {
                    File.WriteAllBytes({|SES1304:entry.Name + destDir|}, bytes);
                }
            }
            """);

    /// <summary>Verifies a sink-named call with no arguments is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SinkNamedZeroArgumentCallIsCleanAsync()
        => await VerifyNet90Async(
            """
            public sealed class Widget
            {
                public void Create()
                {
                }

                public void Create(int flags)
                {
                }
            }

            public class C
            {
                public void M(Widget widget)
                {
                    widget.Create();
                    widget.Create(flags: 3);
                }
            }
            """);

    /// <summary>Verifies a destination read from a plain variable (not an inline join) is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task PlainDestinationVariableIsCleanAsync()
        => await VerifyNet90Async(
            """
            using System.IO;
            using System.IO.Compression;

            public class C
            {
                public void M(ZipArchiveEntry entry, string fullPath, byte[] bytes)
                {
                    File.WriteAllBytes(fullPath, bytes);
                }
            }
            """);

    /// <summary>Verifies a same-named write method on an unrelated type (not <c>System.IO.File</c>) is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task UnrelatedWriteMethodIsCleanAsync()
        => await VerifyNet90Async(
            """
            using System.IO;
            using System.IO.Compression;

            public sealed class Journal
            {
                public void WriteAllBytes(string path, byte[] bytes)
                {
                }
            }

            public class C
            {
                public void M(ZipArchiveEntry entry, string destDir, byte[] bytes, Journal journal)
                {
                    journal.WriteAllBytes(Path.Combine(destDir, entry.FullName), bytes);
                }
            }
            """);

    /// <summary>Verifies a same-named <c>Combine</c> on an unrelated type (not <c>System.IO.Path</c>) is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task UnrelatedCombineIsCleanAsync()
        => await VerifyNet90Async(
            """
            using System.IO;
            using System.IO.Compression;

            public static class Paths
            {
                public static string Combine(string a, string b) => a + b;
            }

            public class C
            {
                public void M(ZipArchiveEntry entry, string destDir, byte[] bytes)
                {
                    File.WriteAllBytes(Paths.Combine(destDir, entry.FullName), bytes);
                }
            }
            """);

    /// <summary>Verifies a look-alike <c>FileStream</c> from another namespace is not reported (the type is bound, not name-matched).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task LookAlikeFileStreamIsCleanAsync()
        => await VerifyNet90Async(
            """
            using System.IO;
            using System.Formats.Tar;

            namespace Other
            {
                public sealed class FileStream
                {
                    public FileStream(string path)
                    {
                    }
                }

                public class C
                {
                    public void M(TarEntry entry, string destDir)
                    {
                        var stream = new FileStream(Path.Combine(destDir, entry.Name));
                    }
                }
            }
            """);

    /// <summary>Verifies a non-<c>FileStream</c> object creation taking a joined entry name is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NonFileStreamCreationIsCleanAsync()
        => await VerifyNet90Async(
            """
            using System.IO;
            using System.Formats.Tar;

            public class C
            {
                public void M(TarEntry entry, string destDir)
                {
                    using var writer = new StreamWriter(Path.Combine(destDir, entry.Name));
                }
            }
            """);

    /// <summary>Verifies a same-named <c>Name</c> field (not a property) is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NameFieldIsCleanAsync()
        => await VerifyNet90Async(
            """
            using System.IO;

            public sealed class Bag
            {
                public string Name = string.Empty;
            }

            public class C
            {
                public void M(Bag bag, string destDir, byte[] bytes)
                {
                    File.WriteAllBytes(Path.Combine(destDir, bag.Name), bytes);
                }
            }
            """);

    /// <summary>Verifies a Tar entry name joined into a fully-qualified <c>new System.IO.FileStream</c> is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task TarEntryIntoQualifiedFileStreamReportedAsync()
        => await VerifyNet90Async(
            """
            using System.IO;
            using System.Formats.Tar;

            public class C
            {
                public void M(TarEntry entry, string destDir)
                {
                    using var stream = new System.IO.FileStream({|SES1304:Path.Combine(destDir, entry.Name)|}, FileMode.Create);
                }
            }
            """);

    /// <summary>Verifies an object creation of a generic type is not reported (the type name is not a guarded sink).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task GenericTypeCreationIsCleanAsync()
        => await VerifyNet90Async(
            """
            using System.Collections.Generic;
            using System.IO;
            using System.Formats.Tar;

            public class C
            {
                public void M(TarEntry entry, string destDir)
                {
                    var list = new List<string>(1) { Path.Combine(destDir, entry.Name) };
                }
            }
            """);

    /// <summary>Verifies the rule stays silent on a framework without either archive-entry type (net40 stubs).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SilentWhenArchiveTypesUnavailableAsync()
    {
        const string Source = """
                              public static class Path
                              {
                                  public static string Combine(string a, string b) => a + b;
                              }

                              public static class File
                              {
                                  public static void WriteAllBytes(string path, byte[] bytes)
                                  {
                                  }
                              }

                              public sealed class ZipArchiveEntry
                              {
                                  public string FullName => string.Empty;
                              }

                              public class C
                              {
                                  public void M(ZipArchiveEntry entry, string destDir, byte[] bytes)
                                  {
                                      File.WriteAllBytes(Path.Combine(destDir, entry.FullName), bytes);
                                  }
                              }
                              """;

        var test = new AnalyzePathTraversal.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.NetFramework.Net40.Default,
            TestCode = Source
        };

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Runs an analyzer-only verification against the .NET 9 reference assemblies (where both archive-entry types exist).</summary>
    /// <param name="source">The source with diagnostic markup.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task VerifyNet90Async(string source)
    {
        var test = new AnalyzePathTraversal.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = source
        };

        await test.RunAsync(CancellationToken.None);
    }
}
