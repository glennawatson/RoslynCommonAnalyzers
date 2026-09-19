// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using Verify = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    StyleSharp.Analyzers.Sst1449NoConsoleOutputAnalyzer>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Tests for <see cref="Sst1449NoConsoleOutputAnalyzer"/> (SST1449 direct console output).</summary>
public class Sst1449NoConsoleOutputAnalyzerUnitTest
{
    /// <summary>Verifies writes through the console's standard writers are reported.</summary>
    /// <param name="writer">The standard output or error writer.</param>
    /// <param name="method">The write method.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    [Arguments("System.Console.Out", "Write")]
    [Arguments("System.Console.Out", "WriteLine")]
    [Arguments("global::System.Console.Error", "Write")]
    [Arguments("global::System.Console.Error", "WriteLine")]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task ConsoleWriterIsReportedAsync(string writer, string method) =>
        Verify.VerifyAnalyzerAsync($$"""
            public class C
            {
                public void M() => {|SST1449:{{writer}}.{{method}}("text")|};
            }
            """);

    /// <summary>Verifies a similarly named writer on a user type is not console output.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task UserConsoleWriterIsCleanAsync() =>
        Verify.VerifyAnalyzerAsync("""
            public static class Console
            {
                public static System.IO.TextWriter Out => System.IO.TextWriter.Null;
            }

            public class C
            {
                public void M() => Console.Out.WriteLine("text");
            }
            """);

    /// <summary>Verifies an extension overload on a standard writer is not treated as a built-in write.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task ConsoleWriterExtensionOverloadIsCleanAsync() =>
        Verify.VerifyAnalyzerAsync("""
            using System.IO;

            public static class WriterExtensions
            {
                public static void Write(this TextWriter writer, ref int value) { }
            }

            public class C
            {
                public void M()
                {
                    var value = 0;
                    System.Console.Out.Write(ref value);
                }
            }
            """);

    /// <summary>Verifies Console.WriteLine is flagged.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task ConsoleWriteLineIsFlaggedAsync() =>
        Verify.VerifyAnalyzerAsync(
            """
            using System;

            public class C
            {
                public void M() => {|SST1449:Console.WriteLine("text")|};
            }
            """);

    /// <summary>Verifies Console.Write and the fully qualified spelling are flagged.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task QualifiedConsoleWriteIsFlaggedAsync() =>
        Verify.VerifyAnalyzerAsync(
            """
            public class C
            {
                public void M() => {|SST1449:System.Console.Write("text")|};
            }
            """);

    /// <summary>Verifies write methods on other types are clean.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task OtherWritersAreCleanAsync() =>
        Verify.VerifyAnalyzerAsync(
            """
            using System.IO;

            public class C
            {
                public void M(TextWriter writer) => writer.WriteLine("text");
            }
            """);

    /// <summary>Verifies non-write console members are clean.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task ConsoleReadIsCleanAsync() =>
        Verify.VerifyAnalyzerAsync(
            """
            using System;

            public class C
            {
                public string M() => Console.ReadLine();
            }
            """);

    /// <summary>Verifies a user type named Console is clean.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task UserConsoleTypeIsCleanAsync() =>
        Verify.VerifyAnalyzerAsync(
            """
            public static class Console
            {
                public static void WriteLine(string text)
                {
                }
            }

            public class C
            {
                public void M() => Console.WriteLine("text");
            }
            """);
}
