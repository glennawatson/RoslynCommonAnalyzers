// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Testing;

using AnalyzeUpload = SecuritySharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    SecuritySharp.Analyzers.Ses1706UnboundedBrowserFileReadAnalyzer>;

namespace SecuritySharp.Analyzers.Tests;

/// <summary>Unit tests for SES1706 (a Blazor uploaded-file read must have a bounded, server-chosen size limit).</summary>
public class UnboundedBrowserFileReadAnalyzerUnitTest
{
    /// <summary>The inline stub of the Blazor <c>IBrowserFile</c> surface the rule gates on.</summary>
    private const string BrowserFileStub =
        """

        namespace Microsoft.AspNetCore.Components.Forms
        {
            public interface IBrowserFile
            {
                long Size { get; }

                System.IO.Stream OpenReadStream(long maxAllowedSize = 512000, System.Threading.CancellationToken cancellationToken = default);
            }
        }
        """;

    /// <summary>Verifies an unbounded <c>long.MaxValue</c> size limit is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task LongMaxValueReportedAsync()
        => await VerifyAsync(
            """
            using Microsoft.AspNetCore.Components.Forms;

            public class C
            {
                public void Read(IBrowserFile file) => file.OpenReadStream({|SES1706:long.MaxValue|});
            }
            """);

    /// <summary>Verifies the client-reported <c>IBrowserFile.Size</c> size limit is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ClientReportedSizeReportedAsync()
        => await VerifyAsync(
            """
            using Microsoft.AspNetCore.Components.Forms;

            public class C
            {
                public void Read(IBrowserFile file) => file.OpenReadStream({|SES1706:file.Size|});
            }
            """);

    /// <summary>Verifies a constant size above the default ceiling is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ConstantAboveCeilingReportedAsync()
        => await VerifyAsync(
            """
            using Microsoft.AspNetCore.Components.Forms;

            public class C
            {
                public void Read(IBrowserFile file) => file.OpenReadStream({|SES1706:20_000_000|});
            }
            """);

    /// <summary>Verifies a size one byte above the default ceiling is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ConstantJustAboveCeilingReportedAsync()
        => await VerifyAsync(
            """
            using Microsoft.AspNetCore.Components.Forms;

            public class C
            {
                public void Read(IBrowserFile file) => file.OpenReadStream({|SES1706:10 * 1024 * 1024 + 1|});
            }
            """);

    /// <summary>Verifies a named <c>maxAllowedSize:</c> unbounded argument is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NamedMaxAllowedSizeReportedAsync()
        => await VerifyAsync(
            """
            using Microsoft.AspNetCore.Components.Forms;
            using System.Threading;

            public class C
            {
                public void Read(IBrowserFile file, CancellationToken token)
                    => file.OpenReadStream(cancellationToken: token, maxAllowedSize: {|SES1706:long.MaxValue|});
            }
            """);

    /// <summary>Verifies an unbounded read on a concrete <c>IBrowserFile</c> implementation is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ConcreteImplementationReportedAsync()
        => await VerifyAsync(
            """
            using Microsoft.AspNetCore.Components.Forms;
            using System.IO;
            using System.Threading;

            public sealed class UploadedFile : IBrowserFile
            {
                public long Size => 0;

                public Stream OpenReadStream(long maxAllowedSize = 512000, CancellationToken cancellationToken = default) => Stream.Null;
            }

            public class C
            {
                public void Read(UploadedFile file) => file.OpenReadStream({|SES1706:long.MaxValue|});
            }
            """);

    /// <summary>Verifies the no-argument <c>OpenReadStream()</c> (the safe default) is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NoArgumentDefaultCleanAsync()
        => await VerifyAsync(
            """
            using Microsoft.AspNetCore.Components.Forms;

            public class C
            {
                public void Read(IBrowserFile file) => file.OpenReadStream();
            }
            """);

    /// <summary>Verifies a small bounded constant size is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SmallBoundedConstantCleanAsync()
        => await VerifyAsync(
            """
            using Microsoft.AspNetCore.Components.Forms;

            public class C
            {
                public void Read(IBrowserFile file) => file.OpenReadStream(1024);
            }
            """);

    /// <summary>Verifies a size exactly at the default ceiling is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ConstantAtCeilingCleanAsync()
        => await VerifyAsync(
            """
            using Microsoft.AspNetCore.Components.Forms;

            public class C
            {
                public void Read(IBrowserFile file) => file.OpenReadStream(10 * 1024 * 1024);
            }
            """);

    /// <summary>Verifies a named <c>maxAllowedSize:</c> bounded constant is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NamedBoundedConstantCleanAsync()
        => await VerifyAsync(
            """
            using Microsoft.AspNetCore.Components.Forms;

            public class C
            {
                public void Read(IBrowserFile file) => file.OpenReadStream(maxAllowedSize: 500_000);
            }
            """);

    /// <summary>Verifies passing only a named <c>cancellationToken:</c> (leaving the safe default) is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task OnlyCancellationTokenCleanAsync()
        => await VerifyAsync(
            """
            using Microsoft.AspNetCore.Components.Forms;
            using System.Threading;

            public class C
            {
                public void Read(IBrowserFile file, CancellationToken token) => file.OpenReadStream(cancellationToken: token);
            }
            """);

    /// <summary>Verifies a non-constant size that is not the client-reported <c>Size</c> is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NonConstantServerChosenSizeCleanAsync()
        => await VerifyAsync(
            """
            using Microsoft.AspNetCore.Components.Forms;

            public class C
            {
                public void Read(IBrowserFile file, long configuredLimit) => file.OpenReadStream(configuredLimit);
            }
            """);

    /// <summary>Verifies a <c>Size</c> property on an unrelated type is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task UnrelatedSizePropertyCleanAsync()
        => await VerifyAsync(
            """
            using Microsoft.AspNetCore.Components.Forms;

            public sealed class Quota
            {
                public long Size => 1024;
            }

            public class C
            {
                public void Read(IBrowserFile file, Quota quota) => file.OpenReadStream(quota.Size);
            }
            """);

    /// <summary>Verifies a lowered rule-specific ceiling reports a size the default would have allowed.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task LoweredRuleCeilingReportsBoundedSizeAsync()
    {
        var test = new AnalyzeUpload.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = """
                       using Microsoft.AspNetCore.Components.Forms;

                       public class C
                       {
                           public void Read(IBrowserFile file) => file.OpenReadStream({|SES1706:2048|});
                       }
                       """ + BrowserFileStub,
        };

        test.TestState.AnalyzerConfigFiles.Add(
            ("/.editorconfig", """
            root = true
            [*.cs]
            securitysharp.SES1706.max_bytes = 1024

            """));

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies a raised project-wide ceiling allows a size the default would flag.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task RaisedProjectWideCeilingAllowsSizeAsync()
    {
        var test = new AnalyzeUpload.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = """
                       using Microsoft.AspNetCore.Components.Forms;

                       public class C
                       {
                           public void Read(IBrowserFile file) => file.OpenReadStream(20_000_000);
                       }
                       """ + BrowserFileStub,
        };

        test.TestState.AnalyzerConfigFiles.Add(
            ("/.editorconfig", """
            root = true
            [*.cs]
            securitysharp.max_bytes = 52428800

            """));

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies the rule stays silent when <c>IBrowserFile</c> is absent from the compilation.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SilentWhenBrowserFileUnavailableAsync()
    {
        const string Source = """
                              public interface IBrowserFile
                              {
                                  long Size { get; }

                                  System.IO.Stream OpenReadStream(long maxAllowedSize = 512000);
                              }

                              public class C
                              {
                                  public void Read(IBrowserFile file) => file.OpenReadStream(long.MaxValue);
                              }
                              """;

        var test = new AnalyzeUpload.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = Source,
        };

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Runs an analyzer-only verification with the inline Blazor <c>IBrowserFile</c> stub appended.</summary>
    /// <param name="source">The source with diagnostic markup.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task VerifyAsync(string source)
    {
        var test = new AnalyzeUpload.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = source + BrowserFileStub,
        };

        await test.RunAsync(CancellationToken.None);
    }
}
