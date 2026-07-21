// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;

using VerifyAnalyzer = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    StyleSharp.Analyzers.Sst2706StaThreadEntryPointAnalyzer>;
using VerifyFix = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.Sst2706StaThreadEntryPointAnalyzer,
    StyleSharp.Analyzers.Sst2706StaThreadEntryPointCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST2706 (a Windows Forms entry point must declare an apartment state).</summary>
public class Sst2706StaThreadEntryPointAnalyzerUnitTest
{
    /// <summary>The inline stub of the Windows Forms application type the rule gates on.</summary>
    private const string WindowsFormsStub =
        """

        namespace System.Windows.Forms
        {
            public sealed class Application
            {
                public static void Run(object form)
                {
                }
            }
        }
        """;

    /// <summary>Verifies an entry point without an apartment attribute is reported and the fix adds [STAThread].</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task EntryPointWithoutApartmentIsReportedAndFixedAsync()
    {
        const string source =
            """
            public static class Program
            {
                public static void {|SST2706:Main|}()
                {
                    System.Windows.Forms.Application.Run(new object());
                }
            }
            """ + WindowsFormsStub;

        const string fixedSource =
            """
            public static class Program
            {
                [System.STAThread]
                public static void Main()
                {
                    System.Windows.Forms.Application.Run(new object());
                }
            }
            """ + WindowsFormsStub;

        var test = new VerifyFix.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = source,
            FixedCode = fixedSource,
        };
        test.TestState.OutputKind = OutputKind.ConsoleApplication;
        test.FixedState.OutputKind = OutputKind.ConsoleApplication;

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies an entry point already marked [STAThread] is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task EntryPointWithStaThreadIsCleanAsync()
        => await VerifyCleanAsync(
            """
            public static class Program
            {
                [System.STAThread]
                public static void Main()
                {
                    System.Windows.Forms.Application.Run(new object());
                }
            }
            """);

    /// <summary>Verifies an entry point already marked [MTAThread] is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task EntryPointWithMtaThreadIsCleanAsync()
        => await VerifyCleanAsync(
            """
            public static class Program
            {
                [System.MTAThread]
                public static void Main()
                {
                    System.Windows.Forms.Application.Run(new object());
                }
            }
            """);

    /// <summary>Verifies a <c>Main</c> method that is not the compilation's entry point is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    /// <remarks>The real entry point (missing its attribute) is reported; the same-named non-entry method is left alone.</remarks>
    [Test]
    public async Task NonEntryPointMainIsNotReportedAsync()
    {
        const string source =
            """
            public static class Program
            {
                public static void {|SST2706:Main|}()
                {
                    System.Windows.Forms.Application.Run(new object());
                }
            }

            public static class Helper
            {
                public static void Main(string label)
                {
                    System.Console.WriteLine(label);
                }
            }
            """ + WindowsFormsStub;

        var test = new VerifyAnalyzer.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = source,
        };
        test.TestState.OutputKind = OutputKind.ConsoleApplication;

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies a project that does not reference Windows Forms is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task WithoutWindowsFormsReferenceIsCleanAsync()
    {
        var test = new VerifyAnalyzer.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode =
                """
                public static class Program
                {
                    public static void Main()
                    {
                        System.Console.WriteLine("no winforms here");
                    }
                }
                """,
        };
        test.TestState.OutputKind = OutputKind.ConsoleApplication;

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Runs an analyzer-only verification of an executable with the Windows Forms stub appended.</summary>
    /// <param name="entryPointSource">The program source (without the stub) that should produce no diagnostic.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task VerifyCleanAsync(string entryPointSource)
    {
        var test = new VerifyAnalyzer.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = entryPointSource + WindowsFormsStub,
        };
        test.TestState.OutputKind = OutputKind.ConsoleApplication;

        await test.RunAsync(CancellationToken.None);
    }
}
