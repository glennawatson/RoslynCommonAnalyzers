// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using Verify = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    StyleSharp.Analyzers.Sst1108EntireFileCommentedOutAnalyzer>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Tests for SST1108 (files containing only commented-out C# code).</summary>
public class Sst1108EntireFileCommentedOutAnalyzerUnitTest
{
    /// <summary>Verifies a commented namespace, type, and method are reported once.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task CommentedNamespaceTypeAndMethodAreReportedAsync() =>
        Verify.VerifyAnalyzerAsync(
            """
            // Copyright information.
            {|SST1108:// namespace Example|}
            // {
            //     public class C
            //     {
            //         public void M()
            //         {
            //             return;
            //         }
            //     }
            // }
            """);

    /// <summary>Verifies a commented type without a namespace is reported.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task CommentedTypeWithoutNamespaceIsReportedAsync() =>
        Verify.VerifyAnalyzerAsync(
            """
            // Copyright information.
            {|SST1108:// public class Processor|}
            // {
            //     public void Run() { }
            // }
            """);

    /// <summary>Verifies common type modifiers remain part of the reconstructed source shape.</summary>
    /// <param name="declaration">The commented type declaration.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    [Arguments("static class Processor")]
    [Arguments("sealed class Processor")]
    [Arguments("partial class Processor")]
    [Arguments("abstract class Processor")]
    [Arguments("readonly struct Processor")]
    [Arguments("file class Processor")]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task CommentedTypeModifiersAreReportedAsync(string declaration) =>
        Verify.VerifyAnalyzerAsync($"{{|SST1108:// {declaration}|}}\n// {{ }}\n");

    /// <summary>Verifies a block comment containing a complete source file is reported.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task CommentedBlockIsReportedAsync() =>
        Verify.VerifyAnalyzerAsync(
            """
            {|SST1108:/* namespace Example
            {
                public class C { }
            } */|}
            """);

    /// <summary>Verifies a block license header is skipped before a commented type is reconstructed.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task CommentedTypeWithBlockLicenseHeaderIsReportedAsync() =>
        Verify.VerifyAnalyzerAsync(
            """
            /* Copyright (c) 2026 Example. */
            {|SST1108:// public class Processor|}
            // {
            //     public void Run() { }
            // }
            """);

    /// <summary>Verifies headers, prose, inactive code, and live declarations remain clean.</summary>
    /// <param name="source">The source file under test.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    [Arguments("// Copyright information.\n// See the license for details.")]
    [Arguments("// This prose ends with a semicolon;\n// It describes the example, not source.")]
    [Arguments("// public policy is documented here.")]
    [Arguments("// class C {")]
    [Arguments("// using Example;")]
    [Arguments("/// <summary>A reference example.</summary>")]
    [Arguments("/* Copyright (c) 2026 Example. */\n/* See the license for details. */")]
    [Arguments("// Example: return;\npublic class C { }")]
    [Arguments("#if false\n// namespace Example\n// {\n// public class C { }\n// }\n#endif")]
    [Arguments("#nullable enable\n// class C { }")]
    [Arguments("")]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task NonSourceCommentsAndLiveCodeAreCleanAsync(string source) =>
        Verify.VerifyAnalyzerAsync(source);
}
