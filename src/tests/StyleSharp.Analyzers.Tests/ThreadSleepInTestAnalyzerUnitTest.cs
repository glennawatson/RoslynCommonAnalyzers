// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Testing;

using Verify = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<StyleSharp.Analyzers.Sst2506ThreadSleepInTestAnalyzer>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST2506 (a <c>Thread.Sleep</c> inside a test method body).</summary>
public class ThreadSleepInTestAnalyzerUnitTest
{
    /// <summary>Verifies a fully-qualified <c>Thread.Sleep</c> in an xUnit fact is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ThreadSleepInXunitFactIsFlaggedAsync()
        => await VerifyAsync(
            """
            using Xunit;

            namespace Xunit { using System; public sealed class FactAttribute : Attribute { } }

            public class C
            {
                [Fact]
                public void M()
                {
                    {|SST2506:System.Threading.Thread.Sleep(100)|};
                }
            }
            """);

    /// <summary>Verifies a <c>Thread.Sleep</c> nested inside an <c>if</c> is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ThreadSleepNestedInIfIsFlaggedAsync()
        => await VerifyAsync(
            """
            using System.Threading;
            using Xunit;

            namespace Xunit { using System; public sealed class FactAttribute : Attribute { } }

            public class C
            {
                [Fact]
                public void M(bool wait)
                {
                    if (wait)
                    {
                        {|SST2506:Thread.Sleep(100)|};
                    }
                }
            }
            """);

    /// <summary>Verifies a <c>Sleep</c> reached through <c>using static Thread</c> is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task UsingStaticSleepIsFlaggedAsync()
        => await VerifyAsync(
            """
            using static System.Threading.Thread;
            using Xunit;

            namespace Xunit { using System; public sealed class FactAttribute : Attribute { } }

            public class C
            {
                [Fact]
                public void M()
                {
                    {|SST2506:Sleep(100)|};
                }
            }
            """);

    /// <summary>Verifies a <c>Thread.Sleep</c> in an NUnit test is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ThreadSleepInNUnitTestIsFlaggedAsync()
        => await VerifyAsync(
            """
            using NUnit.Framework;

            namespace NUnit.Framework { using System; public sealed class TestAttribute : Attribute { } }

            public class C
            {
                [Test]
                public void M()
                {
                    {|SST2506:System.Threading.Thread.Sleep(100)|};
                }
            }
            """);

    /// <summary>Verifies an attribute keeping a known spelling but deriving from a marker is treated as a test.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task DerivedTestAttributeIsFlaggedAsync()
        => await VerifyAsync(
            """
            namespace Xunit { using System; public class FactAttribute : Attribute { } }

            namespace Suite { public class FactAttribute : Xunit.FactAttribute { } }

            public class C
            {
                [Suite.Fact]
                public void M()
                {
                    {|SST2506:System.Threading.Thread.Sleep(100)|};
                }
            }
            """);

    /// <summary>Verifies a test method with no sleep is never reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task TestWithoutSleepIsCleanAsync()
        => await VerifyAsync(
            """
            using Xunit;

            namespace Xunit { using System; public sealed class FactAttribute : Attribute { } }

            public class C
            {
                [Fact]
                public void M()
                {
                    System.GC.KeepAlive(this);
                }
            }
            """);

    /// <summary>Verifies a <c>Thread.Sleep</c> outside any test method is never reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ThreadSleepInNonTestMethodIsCleanAsync()
        => await VerifyAsync(
            """
            namespace Xunit { using System; public sealed class FactAttribute : Attribute { } }

            public class C
            {
                public void Helper()
                {
                    System.Threading.Thread.Sleep(100);
                }
            }
            """);

    /// <summary>Verifies a same-named user <c>Sleep</c> method called from a test is never reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task UserDefinedSleepInTestIsCleanAsync()
        => await VerifyAsync(
            """
            using Xunit;

            namespace Xunit { using System; public sealed class FactAttribute : Attribute { } }

            public class C
            {
                private static void Sleep(int milliseconds) { }

                [Fact]
                public void M()
                {
                    Sleep(100);
                }
            }
            """);

    /// <summary>Verifies a same-named user attribute that is not a marker leaves a test's sleep alone.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SameNamedUserFactAttributeIsCleanAsync()
        => await VerifyAsync(
            """
            namespace Xunit { using System; public sealed class FactAttribute : Attribute { } }

            namespace Mine { using System; public sealed class FactAttribute : Attribute { } }

            public class C
            {
                [Mine.Fact]
                public void M()
                {
                    System.Threading.Thread.Sleep(100);
                }
            }
            """);

    /// <summary>Verifies nothing is reported when no test framework is referenced.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NoTestFrameworkIsCleanAsync()
        => await VerifyAsync(
            """
            public class C
            {
                public void M()
                {
                    System.Threading.Thread.Sleep(100);
                }
            }
            """);

    /// <summary>Runs analyzer verification against the .NET 9 reference assemblies.</summary>
    /// <param name="source">The source with any diagnostic markup.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task VerifyAsync(string source)
    {
        var test = new Verify.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = source,
        };

        await test.RunAsync(CancellationToken.None);
    }
}
