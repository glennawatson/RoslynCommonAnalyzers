// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Verify = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    StyleSharp.Analyzers.Sst2490MergeableAdjacentTryAnalyzer>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST2490 (two adjacent try statements with identical catch/finally handling).</summary>
public class MergeableAdjacentTryAnalyzerUnitTest
{
    /// <summary>Verifies two adjacent tries with an identical typed catch report the second.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task IdenticalTypedCatchIsReportedAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                private int _n;

                public void M()
                {
                    try { _n = 1; } catch (System.InvalidOperationException) { _n = -1; }
                    {|SST2490:try|} { _n = 2; } catch (System.InvalidOperationException) { _n = -1; }
                }
            }
            """);

    /// <summary>Verifies an identical general catch that carries a body is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task IdenticalGeneralCatchBodyIsReportedAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                private int _n;

                public void M()
                {
                    try { _n = 1; } catch { _n = -1; }
                    {|SST2490:try|} { _n = 2; } catch { _n = -1; }
                }
            }
            """);

    /// <summary>Verifies a typed catch with an empty body is reported (catching the type is real handling).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task TypedCatchEmptyBodyIsReportedAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                private int _n;

                public void M()
                {
                    try { _n = 1; } catch (System.InvalidOperationException) { }
                    {|SST2490:try|} { _n = 2; } catch (System.InvalidOperationException) { }
                }
            }
            """);

    /// <summary>Verifies an identical exception filter with an empty body is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task IdenticalFilterEmptyBodyIsReportedAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                private int _n;

                private bool Ok() => _n > 0;

                public void M()
                {
                    try { _n = 1; } catch when (Ok()) { }
                    {|SST2490:try|} { _n = 2; } catch when (Ok()) { }
                }
            }
            """);

    /// <summary>Verifies two adjacent tries with an identical non-empty finally are reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task IdenticalFinallyIsReportedAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                private int _n;

                public void M()
                {
                    try { _n = 1; } finally { _n = 0; }
                    {|SST2490:try|} { _n = 2; } finally { _n = 0; }
                }
            }
            """);

    /// <summary>Verifies a matching catch and finally together are reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task IdenticalCatchAndFinallyIsReportedAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                private int _n;

                public void M()
                {
                    try { _n = 1; } catch (System.InvalidOperationException) { _n = -1; } finally { _n = 0; }
                    {|SST2490:try|} { _n = 2; } catch (System.InvalidOperationException) { _n = -1; } finally { _n = 0; }
                }
            }
            """);

    /// <summary>Verifies three identical tries in a row report the second and the third.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ThreeIdenticalTriesReportEachFollowerAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                private int _n;

                public void M()
                {
                    try { _n = 1; } catch (System.InvalidOperationException) { _n = -1; }
                    {|SST2490:try|} { _n = 2; } catch (System.InvalidOperationException) { _n = -1; }
                    {|SST2490:try|} { _n = 3; } catch (System.InvalidOperationException) { _n = -1; }
                }
            }
            """);

    /// <summary>Verifies catches over different exception types are left alone.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task DifferentCaughtTypeIsCleanAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                private int _n;

                public void M()
                {
                    try { _n = 1; } catch (System.InvalidOperationException) { _n = -1; }
                    try { _n = 2; } catch (System.ArgumentException) { _n = -1; }
                }
            }
            """);

    /// <summary>Verifies catch bodies that differ are left alone.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task DifferentCatchBodyIsCleanAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                private int _n;

                public void M()
                {
                    try { _n = 1; } catch (System.InvalidOperationException) { _n = -1; }
                    try { _n = 2; } catch (System.InvalidOperationException) { _n = -2; }
                }
            }
            """);

    /// <summary>Verifies a differing catch count is left alone.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task DifferentCatchCountIsCleanAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                private int _n;

                public void M()
                {
                    try { _n = 1; } catch (System.InvalidOperationException) { _n = -1; } catch (System.ArgumentException) { _n = -2; }
                    try { _n = 2; } catch (System.InvalidOperationException) { _n = -1; }
                }
            }
            """);

    /// <summary>Verifies catches whose only difference is the exception variable name are left alone.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task DifferentExceptionVariableNameIsCleanAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                private int _n;

                public void M()
                {
                    try { _n = 1; } catch (System.Exception ex) { _n = ex.HResult; }
                    try { _n = 2; } catch (System.Exception e) { _n = e.HResult; }
                }
            }
            """);

    /// <summary>Verifies a finally on only one of the two tries is left alone.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task FinallyOnlyOnFirstIsCleanAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                private int _n;

                public void M()
                {
                    try { _n = 1; } catch (System.InvalidOperationException) { _n = -1; } finally { _n = 0; }
                    try { _n = 2; } catch (System.InvalidOperationException) { _n = -1; }
                }
            }
            """);

    /// <summary>Verifies a finally on only the second of the two tries is left alone.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task FinallyOnlyOnSecondIsCleanAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                private int _n;

                public void M()
                {
                    try { _n = 1; } catch (System.InvalidOperationException) { _n = -1; }
                    try { _n = 2; } catch (System.InvalidOperationException) { _n = -1; } finally { _n = 0; }
                }
            }
            """);

    /// <summary>Verifies matching catches with differing finally bodies are left alone.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task DifferentFinallyBodyIsCleanAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                private int _n;

                public void M()
                {
                    try { _n = 1; } finally { _n = 0; }
                    try { _n = 2; } finally { _n = 9; }
                }
            }
            """);

    /// <summary>Verifies two adjacent bare try/catch with an empty handler add nothing and are left alone.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task BareEmptyCatchIsCleanAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                private int _n;

                public void M()
                {
                    try { _n = 1; } catch { }
                    try { _n = 2; } catch { }
                }
            }
            """);

    /// <summary>Verifies two adjacent try/finally with an empty finally add nothing and are left alone.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task EmptyFinallyIsCleanAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                private int _n;

                public void M()
                {
                    try { _n = 1; } finally { }
                    try { _n = 2; } finally { }
                }
            }
            """);

    /// <summary>Verifies a try whose next sibling is not a try is left alone.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NextSiblingNotATryIsCleanAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                private int _n;

                public void M()
                {
                    try { _n = 1; } catch (System.InvalidOperationException) { _n = -1; }
                    _n = 5;
                }
            }
            """);

    /// <summary>Verifies two identical tries separated by another statement are left alone.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NonAdjacentIdenticalTriesAreCleanAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                private int _n;

                public void M()
                {
                    try { _n = 1; } catch (System.InvalidOperationException) { _n = -1; }
                    _n = 5;
                    try { _n = 2; } catch (System.InvalidOperationException) { _n = -1; }
                }
            }
            """);

    /// <summary>Verifies a single try is left alone.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SingleTryIsCleanAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                private int _n;

                public void M()
                {
                    try { _n = 1; } catch (System.InvalidOperationException) { _n = -1; }
                }
            }
            """);

    /// <summary>Verifies a try that is an embedded statement (its parent is not a block) is left alone.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task EmbeddedTryWithoutBlockParentIsCleanAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                private int _n;

                public void M(bool flag)
                {
                    if (flag)
                        try { _n = 1; } catch (System.InvalidOperationException) { _n = -1; }
                }
            }
            """);
}
