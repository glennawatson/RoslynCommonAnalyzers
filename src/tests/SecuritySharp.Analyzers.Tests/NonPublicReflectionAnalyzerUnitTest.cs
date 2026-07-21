// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Testing;

using AnalyzeNonPublicReflection = SecuritySharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    SecuritySharp.Analyzers.Ses1406NonPublicReflectionAnalyzer>;

namespace SecuritySharp.Analyzers.Tests;

/// <summary>Unit tests for SES1406 (reflection must not reach non-public members to bypass their declared accessibility).</summary>
public class NonPublicReflectionAnalyzerUnitTest
{
    /// <summary>Verifies a lookup passing the literal <c>BindingFlags.NonPublic</c> is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NonPublicLiteralReportedAsync()
        => await VerifyNet90Async(
            """
            using System;
            using System.Reflection;

            public class C
            {
                public void M(Type t) => {|SES1406:t.GetMethod("secret", BindingFlags.NonPublic)|};
            }
            """);

    /// <summary>Verifies the <c>NonPublic</c> bit folded inside an <c>|</c> expression is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NonPublicInsideOrReportedAsync()
        => await VerifyNet90Async(
            """
            using System;
            using System.Reflection;

            public class C
            {
                public void M(Type t) => {|SES1406:t.GetField("field", BindingFlags.NonPublic | BindingFlags.Instance)|};
            }
            """);

    /// <summary>Verifies a <c>const</c> field whose constant value carries <c>NonPublic</c> is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NonPublicConstantFieldReportedAsync()
        => await VerifyNet90Async(
            """
            using System;
            using System.Reflection;

            public class C
            {
                private const BindingFlags Flags = BindingFlags.NonPublic | BindingFlags.Instance;

                public void M(Type t) => {|SES1406:t.GetProperty("prop", Flags)|};
            }
            """);

    /// <summary>Verifies <c>InvokeMember</c> with <c>NonPublic</c> is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task InvokeMemberNonPublicReportedAsync()
        => await VerifyNet90Async(
            """
            using System;
            using System.Reflection;

            public class C
            {
                public void M(Type t) => {|SES1406:t.InvokeMember("m", BindingFlags.NonPublic | BindingFlags.InvokeMethod, null, null, null)|};
            }
            """);

    /// <summary>Verifies a lookup on a <c>TypeInfo</c> receiver (inheriting the method from <c>Type</c>) is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task TypeInfoReceiverReportedAsync()
        => await VerifyNet90Async(
            """
            using System.Reflection;

            public class C
            {
                public void M(TypeInfo ti) => {|SES1406:ti.GetMethod("secret", BindingFlags.NonPublic)|};
            }
            """);

    /// <summary>Verifies a public-only flags value is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task PublicOnlyFlagsIsCleanAsync()
        => await VerifyNet90Async(
            """
            using System;
            using System.Reflection;

            public class C
            {
                public void M(Type t) => t.GetMethod("Visible", BindingFlags.Public | BindingFlags.Instance);
            }
            """);

    /// <summary>Verifies a lookup overload with no <c>BindingFlags</c> argument is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NoBindingFlagsOverloadIsCleanAsync()
        => await VerifyNet90Async(
            """
            using System;

            public class C
            {
                public void M(Type t) => t.GetMethod("Visible");
            }
            """);

    /// <summary>Verifies a flags value assembled at run time (a non-<c>const</c> field) is out of scope and not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NonConstantFlagsIsCleanAsync()
        => await VerifyNet90Async(
            """
            using System;
            using System.Reflection;

            public class C
            {
                private static readonly BindingFlags Flags = BindingFlags.NonPublic | BindingFlags.Instance;

                public void M(Type t) => t.GetMethod("secret", Flags);
            }
            """);

    /// <summary>Verifies unrelated calls (a local call, a non-lookup member call, and a zero-argument lookup) are not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task UnrelatedCallsAreCleanAsync()
        => await VerifyNet90Async(
            """
            using System;

            public class C
            {
                public void M(Type t)
                {
                    Local();
                    _ = t.ToString();
                    _ = t.GetMethods();
                }

                private static void Local()
                {
                }
            }
            """);

    /// <summary>
    /// Verifies the rule keys on the real <c>System.Reflection.BindingFlags</c> and <c>System.Type</c> markers:
    /// a look-alike type and flags enum carrying a <c>NonPublic</c> member of the same shape are not reported.
    /// </summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task LookalikeTypeAndFlagsAreCleanAsync()
        => await VerifyNet90Async(
            """
            namespace Fake
            {
                public enum BindingFlags
                {
                    Instance = 4,
                    NonPublic = 32,
                }

                public sealed class Type
                {
                    public object GetMethod(string name, BindingFlags flags) => null!;
                }
            }

            public class C
            {
                public object M(Fake.Type t) => t.GetMethod("secret", Fake.BindingFlags.NonPublic);
            }
            """);

    /// <summary>Runs an analyzer-only verification against the .NET 9 reference assemblies.</summary>
    /// <param name="source">The source with diagnostic markup.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task VerifyNet90Async(string source)
    {
        var test = new AnalyzeNonPublicReflection.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = source
        };

        await test.RunAsync(CancellationToken.None);
    }
}
