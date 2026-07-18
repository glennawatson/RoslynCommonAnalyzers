// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Verify = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<StyleSharp.Analyzers.Sst2462NewMemberReducesAccessibilityAnalyzer>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST2462 (a <c>new</c> member that reduces the accessibility of the member it hides).</summary>
public class Sst2462NewMemberReducesAccessibilityAnalyzerUnitTest
{
    /// <summary>Verifies a <c>new private</c> method hiding a <c>public</c> base method is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task PrivateMethodHidingPublicIsReportedAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            public class Base
            {
                public void M()
                {
                }
            }

            public class Derived : Base
            {
                private new void {|SST2462:M|}()
                {
                }
            }
            """);

    /// <summary>Verifies a <c>new internal</c> method hiding a <c>public</c> base method is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task InternalMethodHidingPublicIsReportedAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            public class Base
            {
                public void M()
                {
                }
            }

            public class Derived : Base
            {
                internal new void {|SST2462:M|}()
                {
                }
            }
            """);

    /// <summary>Verifies a <c>new protected</c> method hiding a <c>public</c> base method is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ProtectedMethodHidingPublicIsReportedAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            public class Base
            {
                public void M()
                {
                }
            }

            public class Derived : Base
            {
                protected new void {|SST2462:M|}()
                {
                }
            }
            """);

    /// <summary>Verifies a <c>new private</c> method hiding a <c>protected</c> base method is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task PrivateMethodHidingProtectedIsReportedAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            public class Base
            {
                protected void M()
                {
                }
            }

            public class Derived : Base
            {
                private new void {|SST2462:M|}()
                {
                }
            }
            """);

    /// <summary>Verifies a <c>new private</c> property hiding a <c>public</c> base property is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task PrivatePropertyHidingPublicIsReportedAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            public class Base
            {
                public int P { get; set; }
            }

            public class Derived : Base
            {
                private new int {|SST2462:P|} { get; set; }
            }
            """);

    /// <summary>Verifies a <c>new private</c> field hiding a <c>public</c> base field is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task PrivateFieldHidingPublicIsReportedAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            public class Base
            {
                public int F;
            }

            public class Derived : Base
            {
                private new int {|SST2462:F|};
            }
            """);

    /// <summary>Verifies a <c>new private</c> event hiding a <c>public</c> base event is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task PrivateEventHidingPublicIsReportedAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            using System;

            public class Base
            {
                public event EventHandler E;
            }

            public class Derived : Base
            {
                private new event EventHandler {|SST2462:E|};
            }
            """);

    /// <summary>Verifies a <c>new private static</c> method hiding a <c>public static</c> base method is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task PrivateStaticMethodHidingPublicStaticIsReportedAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            public class Base
            {
                public static void M()
                {
                }
            }

            public class Derived : Base
            {
                private static new void {|SST2462:M|}()
                {
                }
            }
            """);

    /// <summary>Verifies a member that narrows a hidden member declared two levels up is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NarrowingGrandparentMemberIsReportedAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            public class Root
            {
                public void M()
                {
                }
            }

            public class Middle : Root
            {
            }

            public class Leaf : Middle
            {
                private new void {|SST2462:M|}()
                {
                }
            }
            """);

    /// <summary>Verifies a <c>new</c> member of equal accessibility is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task EqualAccessibilityIsCleanAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            public class Base
            {
                public void M()
                {
                }
            }

            public class Derived : Base
            {
                public new void M()
                {
                }
            }
            """);

    /// <summary>Verifies a <c>new</c> member that widens accessibility is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task WiderAccessibilityIsCleanAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            public class Base
            {
                protected void M()
                {
                }
            }

            public class Derived : Base
            {
                public new void M()
                {
                }
            }
            """);

    /// <summary>Verifies an incomparable accessibility change (protected to internal) is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task IncomparableAccessibilityIsCleanAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            public class Base
            {
                protected void M()
                {
                }
            }

            public class Derived : Base
            {
                internal new void M()
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
                public virtual void M()
                {
                }
            }

            public class Derived : Base
            {
                public override void M()
                {
                }
            }
            """);

    /// <summary>Verifies a narrower method that hides nothing (a different signature) is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task DifferentSignatureHidesNothingAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            public class Base
            {
                public void M(int x)
                {
                }
            }

            public class Derived : Base
            {
                private new void M(string x)
                {
                }
            }
            """);

    /// <summary>Verifies a narrower member is not reported when the hidden base member is private and never inherited.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task PrivateBaseMemberIsCleanAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            public class Base
            {
                private void M()
                {
                }
            }

            public class Derived : Base
            {
                private new void M()
                {
                }
            }
            """);

    /// <summary>Verifies a narrower member without the <c>new</c> modifier is left to the compiler and not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NarrowingWithoutNewModifierIsCleanAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            public class Base
            {
                public void M()
                {
                }
            }

            public class Derived : Base
            {
                private void M()
                {
                }
            }
            """);

    /// <summary>Verifies a plain new method that neither hides nor narrows is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task UnrelatedMemberIsCleanAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            public class Base
            {
                public void M()
                {
                }
            }

            public class Derived : Base
            {
                private void Other()
                {
                }
            }
            """);
}
