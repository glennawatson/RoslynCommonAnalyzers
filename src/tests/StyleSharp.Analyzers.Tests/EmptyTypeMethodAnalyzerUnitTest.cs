// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyEmpty = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    StyleSharp.Analyzers.EmptyCodeAnalyzer>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for the opt-in empty-construct rules SST1436, SST1437, and SST1438.</summary>
public class EmptyTypeMethodAnalyzerUnitTest
{
    /// <summary>Verifies an empty class, interface, and method are each reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task EmptyConstructsReportedAsync()
        => await VerifyEmpty.VerifyAnalyzerAsync(
            """
            public class {|SST1436:Empty|}
            {
            }

            public interface {|SST1437:IEmpty|}
            {
            }

            public class Holder
            {
                public void {|SST1438:NoOp|}()
                {
                }
            }
            """);

    /// <summary>Verifies populated types and empty virtual/override hooks are not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task PopulatedAndExcludedConstructsAreCleanAsync()
        => await VerifyEmpty.VerifyAnalyzerAsync(
            """
            public class Populated
            {
                public int Value;
            }

            public interface IWork
            {
                void Run();
            }

            public class Base
            {
                public virtual void Hook()
                {
                }
            }

            public class Derived : Base
            {
                public override void Hook()
                {
                }
            }
            """);
}
