// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Verify = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.Sst1448CallerInfoArgumentAnalyzer,
    StyleSharp.Analyzers.Sst1448CallerInfoArgumentCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Tests for <see cref="Sst1448CallerInfoArgumentAnalyzer"/> (SST1448 explicit caller-info arguments).</summary>
public class Sst1448CallerInfoArgumentAnalyzerUnitTest
{
    /// <summary>An explicit caller-member-name argument.</summary>
    private const string ExplicitMemberNameSource = """
        using System.Runtime.CompilerServices;

        public class C
        {
            public void Log(string message, [CallerMemberName] string caller = "")
            {
            }

            public void M() => Log("text", {|SST1448:"M"|});
        }
        """;

    /// <summary>The explicit-argument source after the fix.</summary>
    private const string ExplicitMemberNameFixed = """
        using System.Runtime.CompilerServices;

        public class C
        {
            public void Log(string message, [CallerMemberName] string caller = "")
            {
            }

            public void M() => Log("text");
        }
        """;

    /// <summary>Verifies an explicit caller-member-name argument is flagged.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task ExplicitCallerMemberNameArgumentIsFlaggedAsync()
        => await Verify.VerifyAnalyzerAsync(ExplicitMemberNameSource);

    /// <summary>Verifies letting the compiler fill the parameter is clean.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task CompilerSuppliedCallerInfoIsCleanAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            using System.Runtime.CompilerServices;

            public class C
            {
                public void Log(string message, [CallerMemberName] string caller = "")
                {
                }

                public void M() => Log("text");
            }
            """);

    /// <summary>Verifies forwarding the enclosing member's caller-info parameter is clean.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task ForwardingCallerInfoParameterIsCleanAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            using System.Runtime.CompilerServices;

            public class C
            {
                public void Log(string message, [CallerMemberName] string caller = "")
                {
                }

                public void Outer(string message, [CallerMemberName] string caller = "") => Log(message, caller);
            }
            """);

    /// <summary>Verifies an explicit caller-line-number argument is flagged, including named form.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task ExplicitCallerLineNumberArgumentIsFlaggedAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            using System.Runtime.CompilerServices;

            public class C
            {
                public void Log(string message, [CallerLineNumber] int line = 0)
                {
                }

                public void M() => Log("text", {|SST1448:line: 42|});
            }
            """);

    /// <summary>Verifies ordinary optional arguments are clean.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task OrdinaryOptionalArgumentIsCleanAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            public class C
            {
                public void Log(string message, int retries = 3)
                {
                }

                public void M() => Log("text", 5);
            }
            """);

    /// <summary>Verifies a name stated inside a constructor is clean, since the compiler supplies <c>.ctor</c>.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task MemberNameStatedInAConstructorIsCleanAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            using System.Runtime.CompilerServices;

            public class C
            {
                public C()
                {
                    Width = Register(nameof(Width));
                    Height = Register(nameof(Height));
                }

                public string Width { get; }

                public string Height { get; }

                private static string Register([CallerMemberName] string name = "") => name;
            }
            """);

    /// <summary>Verifies a name stated about another member is clean, since the compiler supplies this one.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task MemberNameStatedAboutAnotherMemberIsCleanAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            using System.Runtime.CompilerServices;

            public class C
            {
                public string Width => Register(nameof(Height));

                public string Height => "";

                private static string Register([CallerMemberName] string name = "") => name;
            }
            """);

    /// <summary>Verifies an accessor stating its own property is still reported.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task MemberNameStatedInItsOwnAccessorIsReportedAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            using System.Runtime.CompilerServices;

            public class C
            {
                public string Width => Register({|SST1448:nameof(Width)|});

                private static string Register([CallerMemberName] string name = "") => name;
            }
            """);

    /// <summary>Verifies a lambda takes the name from the member containing it.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task MemberNameStatedInsideALambdaFollowsTheEnclosingMemberAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            using System;
            using System.Runtime.CompilerServices;

            public class C
            {
                public void Run()
                {
                    Action inside = () => Register({|SST1448:"Run"|});
                    Action other = () => Register("Elsewhere");
                    inside();
                    other();
                }

                private static string Register([CallerMemberName] string name = "") => name;
            }
            """);

    /// <summary>Verifies the fix removes the explicit argument.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task FixRemovesExplicitArgumentAsync()
        => await Verify.VerifyCodeFixAsync(ExplicitMemberNameSource, ExplicitMemberNameFixed);
}
