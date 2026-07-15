// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyBranches = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<StyleSharp.Analyzers.IdenticalBranchesAnalyzer>;
using VerifyFix = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.IdenticalBranchesAnalyzer,
    StyleSharp.Analyzers.Sst2414DuplicateBranchImplementationCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST2414 (two branches of one conditional share an implementation).</summary>
public class DuplicateBranchImplementationAnalyzerUnitTest
{
    /// <summary>A switch statement whose first and third sections share a body.</summary>
    private const string DuplicateSectionSource = """
        public sealed class C
        {
            public int M(int x)
            {
                switch (x)
                {
                    case 1:
                        return A();
                    case 2:
                        return B();
                    {|SST2414:case 3:|}
                        return A();
                }

                return 0;
            }

            private static int A() => 1;

            private static int B() => 2;
        }
        """;

    /// <summary>The switch after the two sections are merged.</summary>
    private const string DuplicateSectionFixed = """
        public sealed class C
        {
            public int M(int x)
            {
                switch (x)
                {
                    case 1:
                    case 3:
                        return A();
                    case 2:
                        return B();
                }

                return 0;
            }

            private static int A() => 1;

            private static int B() => 2;
        }
        """;

    /// <summary>Verifies two switch sections with the same body are reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task DuplicateSectionIsReportedAsync()
        => await VerifyBranches.VerifyAnalyzerAsync(DuplicateSectionSource);

    /// <summary>Verifies two switch-expression arms with the same value are reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task DuplicateArmIsReportedAsync()
        => await VerifyBranches.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                public string M(int x) => x switch
                {
                    1 => "a",
                    2 => "b",
                    {|SST2414:3|} => "a",
                    _ => "z",
                };
            }
            """);

    /// <summary>Verifies two if-chain branches with the same multi-statement body are reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task DuplicateIfBranchIsReportedAsync()
        => await VerifyBranches.VerifyAnalyzerAsync(
            """
            using System;

            public sealed class C
            {
                public void M(int x, Action a, Action b)
                {
                    if (x == 1)
                    {
                        a();
                        b();
                    }
                    else if (x == 2)
                    {
                        b();
                        a();
                    }
                    else {|SST2414:if|} (x == 3)
                    {
                        a();
                        b();
                    }
                }
            }
            """);

    /// <summary>Verifies a case that shares its body with the default section is clean.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task CaseSharingDefaultBodyIsCleanAsync()
        => await VerifyBranches.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                public int M(int x)
                {
                    switch (x)
                    {
                        case 1:
                            return A();
                        case 2:
                            return B();
                        default:
                            return A();
                    }
                }

                private static int A() => 1;

                private static int B() => 2;
            }
            """);

    /// <summary>Verifies a switch a goto could jump into is left alone.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SwitchWithGotoIsCleanAsync()
        => await VerifyBranches.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                public int M(int x)
                {
                    switch (x)
                    {
                        case 0:
                            goto case 1;
                        case 1:
                            return A();
                        case 2:
                            return B();
                        case 3:
                            return A();
                    }

                    return 0;
                }

                private static int A() => 1;

                private static int B() => 2;
            }
            """);

    /// <summary>Verifies single-statement if branches are below the reporting threshold.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SingleStatementIfBranchesAreCleanAsync()
        => await VerifyBranches.VerifyAnalyzerAsync(
            """
            using System;

            public sealed class C
            {
                public void M(int x, Action a, Action b)
                {
                    if (x == 1)
                    {
                        a();
                    }
                    else if (x == 2)
                    {
                        b();
                    }
                    else if (x == 3)
                    {
                        a();
                    }
                }
            }
            """);

    /// <summary>Verifies a fully duplicated switch is the SST1476 case, not SST2414.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task FullyDuplicatedSwitchIsTheAllBranchesCaseAsync()
        => await VerifyBranches.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                public int M(int x)
                {
                    {|SST1476:switch|} (x)
                    {
                        case 1:
                            return A();
                        case 2:
                            return A();
                        default:
                            return A();
                    }
                }

                private static int A() => 1;
            }
            """);

    /// <summary>Verifies the fix stacks the duplicated section's labels onto the earlier one.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task FixMergesDuplicateSectionsAsync()
        => await VerifyFix.VerifyCodeFixAsync(DuplicateSectionSource, DuplicateSectionFixed);
}
