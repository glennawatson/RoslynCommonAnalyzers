// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyConsumeOnce = PerformanceSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    PerformanceSharp.Analyzers.Psh1316ConsumeValueTaskOnceAnalyzer,
    PerformanceSharp.Analyzers.Psh1316ConsumeValueTaskOnceCodeFixProvider>;

namespace PerformanceSharp.Analyzers.Tests;

/// <summary>Unit tests for PSH1316 (a ValueTask consumed more than once, through a loop or a copy).</summary>
public class Psh1316ConsumeValueTaskOnceAnalyzerUnitTest
{
    /// <summary>A ValueTask awaited in a loop to be fixed.</summary>
    private const string AwaitInLoopSource = """
        using System.Threading.Tasks;

        public class C
        {
            private static ValueTask P() => default;

            public async Task M()
            {
                ValueTask vt = P();
                for (int i = 0; i < 3; i++)
                {
                    await {|PSH1316:vt|};
                }
            }
        }
        """;

    /// <summary>The method after the fix.</summary>
    private const string AwaitInLoopFixed = """
        using System.Threading.Tasks;

        public class C
        {
            private static ValueTask P() => default;

            public async Task M()
            {
                for (int i = 0; i < 3; i++)
                {
                    ValueTask vt = P();
                    await vt;
                }
            }
        }
        """;

    /// <summary>Verifies a ValueTask declared outside a loop and awaited inside it is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task AwaitInLoopReportedAsync()
        => await VerifyConsumeOnce.VerifyAnalyzerAsync(
            """
            using System.Threading.Tasks;

            public class C
            {
                private static ValueTask P() => default;

                public async Task M()
                {
                    ValueTask vt = P();
                    for (int i = 0; i < 3; i++)
                    {
                        await {|PSH1316:vt|};
                    }
                }
            }
            """);

    /// <summary>Verifies a ValueTask copied into a second local, where both are consumed, is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task CopyThenAwaitReportedAsync()
        => await VerifyConsumeOnce.VerifyAnalyzerAsync(
            """
            using System.Threading.Tasks;

            public class C
            {
                private static ValueTask P() => default;

                public async Task M()
                {
                    ValueTask vt = P();
                    ValueTask {|PSH1316:copy|} = vt;
                    await vt;
                    await copy;
                }
            }
            """);

    /// <summary>Verifies a ValueTask created fresh inside the loop is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task FreshInLoopIsCleanAsync()
        => await VerifyConsumeOnce.VerifyAnalyzerAsync(
            """
            using System.Threading.Tasks;

            public class C
            {
                private static ValueTask P() => default;

                public async Task M()
                {
                    for (int i = 0; i < 3; i++)
                    {
                        ValueTask vt = P();
                        await vt;
                    }
                }
            }
            """);

    /// <summary>Verifies a preserved ValueTask awaited in a loop is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task PreservedInLoopIsCleanAsync()
        => await VerifyConsumeOnce.VerifyAnalyzerAsync(
            """
            using System.Threading.Tasks;

            public class C
            {
                private static ValueTask P() => default;

                public async Task M()
                {
                    ValueTask vt = P();
                    vt = vt.Preserve();
                    for (int i = 0; i < 3; i++)
                    {
                        await vt;
                    }
                }
            }
            """);

    /// <summary>Verifies a single consume is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SingleConsumeIsCleanAsync()
        => await VerifyConsumeOnce.VerifyAnalyzerAsync(
            """
            using System.Threading.Tasks;

            public class C
            {
                private static ValueTask P() => default;

                public async Task M()
                {
                    ValueTask vt = P();
                    await vt;
                }
            }
            """);

    /// <summary>Verifies the fix moves the producing call into the loop.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task AwaitInLoopFixedByMovingProducerAsync()
        => await VerifyConsumeOnce.VerifyCodeFixAsync(AwaitInLoopSource, AwaitInLoopFixed);
}
