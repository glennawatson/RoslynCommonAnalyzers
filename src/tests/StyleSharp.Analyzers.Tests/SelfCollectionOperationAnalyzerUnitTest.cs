// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifySelf = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<StyleSharp.Analyzers.Sst2419SelfCollectionOperationAnalyzer>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST2419 (a set or list operation applied to itself).</summary>
public class SelfCollectionOperationAnalyzerUnitTest
{
    /// <summary>Verifies a set unioned with itself is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SetUnionedWithItselfIsReportedAsync()
        => await VerifySelf.VerifyAnalyzerAsync(
            """
            using System.Collections.Generic;

            public sealed class C
            {
                public void M(HashSet<int> set) => {|SST2419:set.UnionWith(set)|};
            }
            """);

    /// <summary>Verifies a list adding its own range is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ListAddingItsOwnRangeIsReportedAsync()
        => await VerifySelf.VerifyAnalyzerAsync(
            """
            using System.Collections.Generic;

            public sealed class C
            {
                public void M(List<int> items) => {|SST2419:items.AddRange(items)|};
            }
            """);

    /// <summary>Verifies a set excepted with itself is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SetExceptedWithItselfIsReportedAsync()
        => await VerifySelf.VerifyAnalyzerAsync(
            """
            using System.Collections.Generic;

            public sealed class C
            {
                public void M(HashSet<int> set) => {|SST2419:set.ExceptWith(set)|};
            }
            """);

    /// <summary>Verifies an operation on two different collections is clean.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task DifferentCollectionsAreCleanAsync()
        => await VerifySelf.VerifyAnalyzerAsync(
            """
            using System.Collections.Generic;

            public sealed class C
            {
                public void M(HashSet<int> a, HashSet<int> b) => a.UnionWith(b);
            }
            """);

    /// <summary>Verifies a call-valued receiver, which may return two different collections, is clean.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task CallValuedReceiverIsCleanAsync()
        => await VerifySelf.VerifyAnalyzerAsync(
            """
            using System.Collections.Generic;

            public sealed class C
            {
                public void M() => Get().UnionWith(Get());

                private HashSet<int> Get() => new HashSet<int>();
            }
            """);
}
