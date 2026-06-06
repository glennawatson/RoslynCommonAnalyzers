// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyQueryClause = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    StyleSharp.Analyzers.QueryClauseAnalyzer>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for the LINQ query-clause layout rules (SST1102–SST1105).</summary>
public class QueryClauseAnalyzerUnitTest
{
    /// <summary>Verifies a blank line between clauses is reported (SST1102).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task BlankLineBetweenClausesReportedAsync()
        => await VerifyQueryClause.VerifyAnalyzerAsync(
            """
            using System.Collections.Generic;
            using System.Linq;

            internal class C
            {
                private static IEnumerable<int> M(int[] source) =>
                    from x in source

                    {|SST1102:where x > 0|}
                    select x;
            }
            """);

    /// <summary>Verifies clauses mixing single-line and multi-line layout are reported (SST1103).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task MixedClauseLayoutReportedAsync()
        => await VerifyQueryClause.VerifyAnalyzerAsync(
            """
            using System.Collections.Generic;
            using System.Linq;

            internal class C
            {
                private static IEnumerable<int> M(int[] source) =>
                    from x in source
                    where x > 0 {|SST1103:select x|};
            }
            """);

    /// <summary>Verifies a clause sharing a multi-line clause's last line is reported (SST1104).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ClauseAfterMultiLineClauseReportedAsync()
        => await VerifyQueryClause.VerifyAnalyzerAsync(
            """
            using System.Collections.Generic;
            using System.Linq;

            internal class C
            {
                private static IEnumerable<int> M(int[] source) =>
                    from x in source
                    where x > 0 &&
                        x < 10 {|SST1104:select x|};
            }
            """);

    /// <summary>Verifies a multi-line clause that does not begin on its own line is reported (SST1105).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task MultiLineClauseNotOnOwnLineReportedAsync()
        => await VerifyQueryClause.VerifyAnalyzerAsync(
            """
            using System.Collections.Generic;
            using System.Linq;

            internal class C
            {
                private static IEnumerable<int> M(int[] source) =>
                    from x in source {|SST1105:where x > 0 &&
                        x < 10|}
                    select x;
            }
            """);

    /// <summary>Verifies clauses each on their own line are not flagged.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ClausesOnSeparateLinesAreCleanAsync()
        => await VerifyQueryClause.VerifyAnalyzerAsync(
            """
            using System.Collections.Generic;
            using System.Linq;

            internal class C
            {
                private static IEnumerable<int> M(int[] source) =>
                    from x in source
                    where x > 0
                    select x;
            }
            """);
}
