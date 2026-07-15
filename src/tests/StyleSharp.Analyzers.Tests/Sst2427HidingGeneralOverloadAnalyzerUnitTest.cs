// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Verify = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<StyleSharp.Analyzers.Sst2427HidingGeneralOverloadAnalyzer>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST2427 (a derived overload hiding a more specific base overload).</summary>
public class Sst2427HidingGeneralOverloadAnalyzerUnitTest
{
    /// <summary>Verifies a derived overload whose parameter is a base type of the base overload's parameter is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task GeneralDerivedOverloadIsReportedAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            public class Base
            {
                public virtual void Handle(string message)
                {
                }
            }

            public class Derived : Base
            {
                public void {|SST2427:Handle|}(object message)
                {
                }
            }
            """);

    /// <summary>Verifies a general overload that hides a base overload two levels up is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task GeneralOverloadHidingGrandparentIsReportedAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            public class Root
            {
                public void Handle(string message)
                {
                }
            }

            public class Middle : Root
            {
            }

            public class Leaf : Middle
            {
                public void {|SST2427:Handle|}(object message)
                {
                }
            }
            """);

    /// <summary>Verifies an <c>override</c> is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task OverrideIsCleanAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            public class Base
            {
                public virtual void Handle(string message)
                {
                }
            }

            public class Derived : Base
            {
                public override void Handle(string message)
                {
                }
            }
            """);

    /// <summary>Verifies a member marked <c>new</c> is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NewModifierIsCleanAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            public class Base
            {
                public virtual void Handle(string message)
                {
                }
            }

            public class Derived : Base
            {
                public new void Handle(object message)
                {
                }
            }
            """);

    /// <summary>Verifies an unrelated parameter type is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task UnrelatedParameterTypeIsCleanAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            public class Base
            {
                public virtual void Handle(string message)
                {
                }
            }

            public class Derived : Base
            {
                public void Handle(int message)
                {
                }
            }
            """);

    /// <summary>Verifies a more specific derived overload is not reported, since it hides nothing.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task MoreSpecificDerivedOverloadIsCleanAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            public class Base
            {
                public virtual void Handle(object message)
                {
                }
            }

            public class Derived : Base
            {
                public void Handle(string message)
                {
                }
            }
            """);

    /// <summary>Verifies a plain new overload that adds a parameter is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task DifferentArityIsCleanAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            public class Base
            {
                public virtual void Handle(string message)
                {
                }
            }

            public class Derived : Base
            {
                public void Handle(object message, int code)
                {
                }
            }
            """);

    /// <summary>Verifies a derived overload whose parameter is a base interface of the base overload's parameter is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task InterfaceGeneralizationIsReportedAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            using System.Collections.Generic;

            public class Base
            {
                public virtual void Handle(List<int> values)
                {
                }
            }

            public class Derived : Base
            {
                public void {|SST2427:Handle|}(IEnumerable<int> values)
                {
                }
            }
            """);

    /// <summary>Verifies a private base overload is not treated as hidden, since it is not inherited.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task PrivateBaseOverloadIsCleanAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            public class Base
            {
                private void Handle(string message)
                {
                }
            }

            public class Derived : Base
            {
                public void Handle(object message)
                {
                }
            }
            """);
}
