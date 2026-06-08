// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Verify = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.MemberDocumentationAnalyzer,
    StyleSharp.Analyzers.DocumentationStubCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for the documentation stub code fixes (SST1611/1615/1617/1618).</summary>
public class DocumentationStubFixUnitTest
{
    /// <summary>Verifies the fix inserts a <c>&lt;param&gt;</c> stub for an undocumented parameter (SST1611).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ParameterAsync()
    {
        const string Source = """
            /// <summary>A container.</summary>
            public class C
            {
                /// <summary>Does a thing.</summary>
                public void M(int {|SST1611:value|}) { }
            }
            """;

        // The stub is a scaffold; the now-empty <param> raises SST1614 to be filled in.
        const string FixedSource = """
            /// <summary>A container.</summary>
            public class C
            {
                /// <summary>Does a thing.</summary>
                /// <param name="value"></param>
                public void M(int {|SST1614:value|}) { }
            }
            """;

        await Verify.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies the fix inserts a <c>&lt;returns&gt;</c> stub for a non-void member (SST1615).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ReturnsAsync()
    {
        const string Source = """
            /// <summary>A container.</summary>
            public class C
            {
                /// <summary>Gets a value.</summary>
                public int {|SST1615:M|}() => 0;
            }
            """;
        const string FixedSource = """
            /// <summary>A container.</summary>
            public class C
            {
                /// <summary>Gets a value.</summary>
                /// {|SST1616:<returns></returns>|}
                public int M() => 0;
            }
            """;

        await Verify.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies the fix inserts a <c>&lt;typeparam&gt;</c> stub (SST1618).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task TypeParameterAsync()
    {
        const string Source = """
            /// <summary>A container.</summary>
            public class C
            {
                /// <summary>Does a thing.</summary>
                public void M<{|SST1618:T|}>() { }
            }
            """;
        const string FixedSource = """
            /// <summary>A container.</summary>
            public class C
            {
                /// <summary>Does a thing.</summary>
                /// <typeparam name="T"></typeparam>
                public void M<{|SST1622:T|}>() { }
            }
            """;

        await Verify.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies the inserted stub adopts the document's CRLF line endings rather than a hard-coded LF.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ParameterStubUsesCrLfAsync()
    {
        const string Source =
            "/// <summary>A container.</summary>\r\npublic class C\r\n{\r\n"
            + "    /// <summary>Does a thing.</summary>\r\n    public void M(int {|SST1611:value|}) { }\r\n}";
        const string FixedSource =
            "/// <summary>A container.</summary>\r\npublic class C\r\n{\r\n"
            + "    /// <summary>Does a thing.</summary>\r\n    /// <param name=\"value\"></param>\r\n"
            + "    public void M(int {|SST1614:value|}) { }\r\n}";

        await Verify.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies the fix removes a stray <c>&lt;returns&gt;</c> from a void member (SST1617).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task RemoveReturnsAsync()
    {
        const string Source = """
            /// <summary>A container.</summary>
            public class C
            {
                /// <summary>Does a thing.</summary>
                /// <returns>Nothing.</returns>
                public void {|SST1617:M|}() { }
            }
            """;
        const string FixedSource = """
            /// <summary>A container.</summary>
            public class C
            {
                /// <summary>Does a thing.</summary>
                public void M() { }
            }
            """;

        await Verify.VerifyCodeFixAsync(Source, FixedSource);
    }
}
