// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Testing;

using Verify = PerformanceSharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    PerformanceSharp.Analyzers.Psh1417ExpensiveDebugAssertArgumentAnalyzer>;

namespace PerformanceSharp.Analyzers.Tests;

/// <summary>Tests for <see cref="Psh1417ExpensiveDebugAssertArgumentAnalyzer"/> (PSH1417 expensive assertion arguments).</summary>
public class ExpensiveDebugAssertArgumentAnalyzerUnitTest
{
    /// <summary>Verifies a call in the assertion condition is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    /// <remarks>
    /// A release build compiles the whole call away, arguments included, so nothing is paid there; a debug
    /// build has to evaluate the condition for the assertion to mean anything. Either way the condition is
    /// not work that gets thrown away. Reported as issue #52.
    /// </remarks>
    [Test]
    public async Task CallInConditionIsNotReportedAsync()
        => await VerifyAsync(
            """
            using System.Collections.Generic;
            using System.Diagnostics;
            using System.Linq;

            public class C
            {
                public void M(IEnumerable<int> source)
                    => Debug.Assert(source.Any(x => x > 0));
            }
            """);

    /// <summary>Verifies an interpolated message is not reported where the framework defers it.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    /// <remarks>
    /// The interpolated-string overload hands the framework a handler that only builds the message once the
    /// condition has already failed, so a passing assertion pays nothing. It is the shape to move towards.
    /// </remarks>
    [Test]
    public async Task InterpolatedMessageIsNotReportedAsync()
        => await VerifyAsync(
            """
            using System.Diagnostics;

            public class C
            {
                public void M(object value)
                    => Debug.Assert(value != null, $"value was {value.ToString()}");
            }
            """);

    /// <summary>Verifies a message built by a call is reported, because a passing assertion still builds it.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task CallInMessageIsReportedAsync()
        => await VerifyAsync(
            """
            using System.Collections.Generic;
            using System.Diagnostics;

            public class C
            {
                public void M(List<string> items)
                    => Debug.Assert(items.Count > 0, {|PSH1417:string.Join(",", items)|});
            }
            """);

    /// <summary>Verifies a cheap comparison is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task CheapConditionIsNotReportedAsync()
        => await VerifyAsync(
            """
            using System.Diagnostics;

            public class C
            {
                public void M(object value) => Debug.Assert(value != null);
            }
            """);

    /// <summary>Verifies a property read is not reported, because it costs nothing worth moving.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task PropertyReadIsNotReportedAsync()
        => await VerifyAsync(
            """
            using System.Collections.Generic;
            using System.Diagnostics;

            public class C
            {
                public void M(List<int> values) => Debug.Assert(values.Count > 0);
            }
            """);

    /// <summary>Verifies a constant message is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ConstantMessageIsNotReportedAsync()
        => await VerifyAsync(
            """
            using System.Diagnostics;

            public class C
            {
                public void M(object value) => Debug.Assert(value != null, "value must not be null");
            }
            """);

    /// <summary>Verifies nameof is not reported: it is an invocation in syntax only, and folds to a constant.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NameOfMessageIsNotReportedAsync()
        => await VerifyAsync(
            """
            using System.Diagnostics;

            public class C
            {
                public void M(object value) => Debug.Assert(value != null, nameof(value));
            }
            """);

    /// <summary>Runs an analyzer verification against the .NET 9 reference assemblies.</summary>
    /// <param name="source">The source with diagnostic markup.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task VerifyAsync(string source)
    {
        var test = new Verify.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = source
        };

        await test.RunAsync(CancellationToken.None);
    }
}
