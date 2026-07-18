// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifySelf = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<StyleSharp.Analyzers.Sst2450DebugAssertSideEffectAnalyzer>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST2450 (a Debug.Assert condition contains a side effect).</summary>
public class DebugAssertSideEffectAnalyzerUnitTest
{
    /// <summary>Verifies a collection mutation used as the whole condition is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task RemoveFromListInConditionIsReportedAsync()
        => await VerifySelf.VerifyAnalyzerAsync(
            """
            using System.Collections.Generic;
            using System.Diagnostics;

            public sealed class C
            {
                public void M(List<int> items, int x) => Debug.Assert({|SST2450:items.Remove(x)|});
            }
            """);

    /// <summary>Verifies a set addition used as the whole condition is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SetAddInConditionIsReportedAsync()
        => await VerifySelf.VerifyAnalyzerAsync(
            """
            using System.Collections.Generic;
            using System.Diagnostics;

            public sealed class C
            {
                public void M(HashSet<int> set, int x) => Debug.Assert({|SST2450:set.Add(x)|});
            }
            """);

    /// <summary>Verifies an enumerator advance used as the condition is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task MoveNextInConditionIsReportedAsync()
        => await VerifySelf.VerifyAnalyzerAsync(
            """
            using System.Collections.Generic;
            using System.Diagnostics;

            public sealed class C
            {
                public void M(IEnumerator<int> e) => Debug.Assert({|SST2450:e.MoveNext()|});
            }
            """);

    /// <summary>Verifies a postfix increment nested in the condition is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task PostfixIncrementInConditionIsReportedAsync()
        => await VerifySelf.VerifyAnalyzerAsync(
            """
            using System.Diagnostics;

            public sealed class C
            {
                public void M(int i) => Debug.Assert({|SST2450:i++|} > 0);
            }
            """);

    /// <summary>Verifies a prefix decrement nested in the condition is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task PrefixDecrementInConditionIsReportedAsync()
        => await VerifySelf.VerifyAnalyzerAsync(
            """
            using System.Diagnostics;

            public sealed class C
            {
                public void M(int n) => Debug.Assert({|SST2450:--n|} >= 0);
            }
            """);

    /// <summary>Verifies a simple assignment nested in the condition is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task AssignmentInConditionIsReportedAsync()
        => await VerifySelf.VerifyAnalyzerAsync(
            """
            using System.Diagnostics;

            public sealed class C
            {
                public void M(int count) => Debug.Assert(({|SST2450:count = Compute()|}) > 0);

                private static int Compute() => 1;
            }
            """);

    /// <summary>Verifies a compound assignment nested in the condition is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task CompoundAssignmentInConditionIsReportedAsync()
        => await VerifySelf.VerifyAnalyzerAsync(
            """
            using System.Diagnostics;

            public sealed class C
            {
                public void M(int total) => Debug.Assert(({|SST2450:total += 1|}) > 0);
            }
            """);

    /// <summary>Verifies a fully qualified Debug.Assert is still reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task FullyQualifiedDebugAssertIsReportedAsync()
        => await VerifySelf.VerifyAnalyzerAsync(
            """
            using System.Collections.Generic;

            public sealed class C
            {
                public void M(List<int> items, int x) => System.Diagnostics.Debug.Assert({|SST2450:items.Remove(x)|});
            }
            """);

    /// <summary>Verifies a using-static Debug.Assert is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task UsingStaticDebugAssertIsReportedAsync()
        => await VerifySelf.VerifyAnalyzerAsync(
            """
            using System.Collections.Generic;
            using static System.Diagnostics.Debug;

            public sealed class C
            {
                public void M(List<int> items, int x) => Assert({|SST2450:items.Remove(x)|});
            }
            """);

    /// <summary>Verifies a plain comparison condition is clean.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ComparisonConditionIsCleanAsync()
        => await VerifySelf.VerifyAnalyzerAsync(
            """
            using System.Diagnostics;

            public sealed class C
            {
                public void M(int x) => Debug.Assert(x > 0);
            }
            """);

    /// <summary>Verifies null and property-read boolean operators are clean.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NullAndPropertyReadConditionIsCleanAsync()
        => await VerifySelf.VerifyAnalyzerAsync(
            """
            using System.Collections.Generic;
            using System.Diagnostics;

            public sealed class C
            {
                public void M(List<int> items) => Debug.Assert(items != null && items.Count > 0);
            }
            """);

    /// <summary>Verifies an ordinary predicate call is not flagged as a side effect.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task PredicateCallConditionIsCleanAsync()
        => await VerifySelf.VerifyAnalyzerAsync(
            """
            using System.Diagnostics;

            public sealed class C
            {
                public void M(int x) => Debug.Assert(IsValid(x));

                private static bool IsValid(int x) => x > 0;
            }
            """);

    /// <summary>Verifies a query call such as Contains is not flagged.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ContainsCallConditionIsCleanAsync()
        => await VerifySelf.VerifyAnalyzerAsync(
            """
            using System.Collections.Generic;
            using System.Diagnostics;

            public sealed class C
            {
                public void M(List<int> items, int x) => Debug.Assert(items.Contains(x));
            }
            """);

    /// <summary>Verifies the idiomatic TryGetValue-in-assert shape is not flagged.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task TryGetValueConditionIsCleanAsync()
        => await VerifySelf.VerifyAnalyzerAsync(
            """
            using System.Collections.Generic;
            using System.Diagnostics;

            public sealed class C
            {
                public void M(Dictionary<int, int> map, int key) => Debug.Assert(map.TryGetValue(key, out var value) && value > 0);
            }
            """);

    /// <summary>Verifies a pattern check that declares a variable is clean.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task PatternConditionIsCleanAsync()
        => await VerifySelf.VerifyAnalyzerAsync(
            """
            using System.Diagnostics;

            public sealed class C
            {
                public void M(object o) => Debug.Assert(o is int n && n > 0);
            }
            """);

    /// <summary>Verifies a nameof expression in the condition is clean.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NameofConditionIsCleanAsync()
        => await VerifySelf.VerifyAnalyzerAsync(
            """
            using System.Diagnostics;

            public sealed class C
            {
                public void M(int value) => Debug.Assert(nameof(value) != null);
            }
            """);

    /// <summary>Verifies a same-named method on another type is not treated as Debug.Assert.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task TraceAssertWithMutationIsCleanAsync()
        => await VerifySelf.VerifyAnalyzerAsync(
            """
            using System.Collections.Generic;
            using System.Diagnostics;

            public sealed class C
            {
                public void M(List<int> items, int x) => Trace.Assert(items.Remove(x));
            }
            """);

    /// <summary>Verifies a user-defined Assert method is not treated as Debug.Assert.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task UserDefinedAssertWithMutationIsCleanAsync()
        => await VerifySelf.VerifyAnalyzerAsync(
            """
            using System.Collections.Generic;

            public sealed class C
            {
                public void M(List<int> items, int x) => Check.Assert(items.Remove(x));
            }

            public static class Check
            {
                public static void Assert(bool condition)
                {
                }
            }
            """);

    /// <summary>Verifies a collection mutation outside any assert is clean.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task MutationOutsideAssertIsCleanAsync()
        => await VerifySelf.VerifyAnalyzerAsync(
            """
            using System.Collections.Generic;

            public sealed class C
            {
                public void M(List<int> items, int x) => items.Remove(x);
            }
            """);
}
