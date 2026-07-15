// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Verify = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    StyleSharp.Analyzers.Sst2425BaseCallDropsOptionalArgumentAnalyzer>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Tests for SST2425 (an override drops an optional argument on the base call).</summary>
public class Sst2425BaseCallDropsOptionalArgumentAnalyzerUnitTest
{
    /// <summary>Verifies a base call that omits the override's optional parameter is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task DroppedOptionalArgumentIsReportedAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            public class Base
            {
                public virtual void Go(int a, string mode = "fast")
                {
                }
            }

            public class Derived : Base
            {
                public override void Go(int a, string mode = "fast")
                {
                    {|SST2425:base.Go(a)|};
                }
            }
            """);

    /// <summary>Verifies forwarding the parameter positionally is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ForwardedPositionallyIsCleanAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            public class Base
            {
                public virtual void Go(int a, string mode = "fast")
                {
                }
            }

            public class Derived : Base
            {
                public override void Go(int a, string mode = "fast")
                {
                    base.Go(a, mode);
                }
            }
            """);

    /// <summary>Verifies forwarding the parameter by name is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ForwardedByNameIsCleanAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            public class Base
            {
                public virtual void Go(int a, string mode = "fast")
                {
                }
            }

            public class Derived : Base
            {
                public override void Go(int a, string mode = "fast")
                {
                    base.Go(a, mode: mode);
                }
            }
            """);

    /// <summary>Verifies a base call to a different method is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task BaseCallToDifferentMethodIsCleanAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            public class Base
            {
                public virtual void Go(int a, string mode = "fast")
                {
                }

                public virtual void Helper()
                {
                }
            }

            public class Derived : Base
            {
                public override void Go(int a, string mode = "fast")
                {
                    base.Helper();
                }
            }
            """);
}
