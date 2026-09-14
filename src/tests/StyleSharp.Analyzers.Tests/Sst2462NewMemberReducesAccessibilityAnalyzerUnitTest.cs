// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using Verify = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<StyleSharp.Analyzers.Sst2462NewMemberReducesAccessibilityAnalyzer>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST2462 (a <c>new</c> member that reduces the accessibility of the member it hides).</summary>
public class Sst2462NewMemberReducesAccessibilityAnalyzerUnitTest
{
    /// <summary>Verifies the strict subset relationship between all declared accessibility levels.</summary>
    /// <param name="inherited">The base member accessibility.</param>
    /// <param name="declared">The hiding member accessibility.</param>
    /// <param name="reports">Whether the hiding member admits fewer callers.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("protected internal", "protected", true)]
    [Arguments("protected internal", "internal", true)]
    [Arguments("protected internal", "private protected", true)]
    [Arguments("public", "protected internal", true)]
    [Arguments("protected", "private protected", true)]
    [Arguments("internal", "private protected", true)]
    [Arguments("private protected", "private", true)]
    [Arguments("private protected", "private protected", false)]
    [Arguments("private protected", "protected", false)]
    [Arguments("internal", "protected", false)]
    [Arguments("protected internal", "public", false)]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task AccessibilityCallerSetsDetermineNarrowingAsync(string inherited, string declared, bool reports) =>
        Verify.VerifyAnalyzerAsync($$"""
            class Base { {{inherited}} void M() { } }
            class Derived : Base { {{declared}} new void {{(reports ? "{|SST2462:M|}" : "M")}}() { } }
            """);

    /// <summary>Verifies signature arity, parameter count, ref kind, and member kind prevent unrelated hiding reports.</summary>
    /// <param name="baseMember">The inherited declaration.</param>
    /// <param name="derivedMember">The declaration with the same name.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("public void M<T>() { }", "private new void M() { }")]
    [Arguments("public void M(int value) { }", "private new void M() { }")]
    [Arguments("public void M(ref int value) { }", "private new void M(int value) { }")]
    [Arguments("public int M;", "private new void M() { }")]
    [Arguments("public void M() { }", "private new int M;")]
    [Arguments("public class M<T> { }", "private new class M { }")]
    [Arguments("public int M;", "private new class M { }")]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task DifferentHidingShapesAreCleanAsync(string baseMember, string derivedMember) =>
        Verify.VerifyAnalyzerAsync($"class Base {{ {baseMember} }} class Derived : Base {{ {derivedMember} }}");

    /// <summary>Verifies matching parameter types and ref kinds permit a narrowing diagnostic.</summary>
    /// <param name="parameters">The signature shared by the base and derived methods.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("int first, string second")]
    [Arguments("ref int first, out string second")]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task MatchingParameterSignaturesAreReportedAsync(string parameters) =>
        Verify.VerifyAnalyzerAsync($$"""
            class Base { public void M<T>({{parameters}}) { second = string.Empty; } }
            class Derived : Base { private new void {|SST2462:M|}<T>({{parameters}}) { second = string.Empty; } }
            """);

    /// <summary>Verifies matching generic nested types are compared by accessibility.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task NestedTypeWithMatchingArityIsReportedAsync() =>
        Verify.VerifyAnalyzerAsync("class Base { public class Nested<T> { } } class Derived : Base { private new class {|SST2462:Nested|}<T> { } }");

    /// <summary>Verifies method type parameters from distinct declarations currently do not match by ordinal.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task GenericParameterSignatureCurrentlyDoesNotReportAsync() =>
        Verify.VerifyAnalyzerAsync("class Base { public void M<T>(T value) { } } class Derived : Base { private new void M<T>(T value) { } }");

    /// <summary>Verifies the nearest matching base declaration stops the search before a wider ancestor.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task EqualNearestBaseMemberStopsSearchAsync() =>
        Verify.VerifyAnalyzerAsync("""
            class Root { public void M() { } }
            class Middle : Root { protected new void {|SST2462:M|}() { } }
            class Leaf : Middle { protected new void M() { } }
            """);

    /// <summary>Verifies a malformed duplicate signature still selects the most accessible inherited candidate.</summary>
    /// <param name="first">The first candidate's accessibility.</param>
    /// <param name="second">The second candidate's accessibility.</param>
    /// <param name="expected">The accessibility named in the diagnostic.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("protected", "public", "public")]
    [Arguments("public", "protected", "public")]
    [Arguments("protected", "protected", "protected")]
    public async Task DuplicateBaseSignaturesSelectWidestAccessibilityAsync(string first, string second, string expected)
    {
        var test = new Verify.Test
        {
            TestCode = $$"""
                class Base { {{first}} void M() { } {{second}} void M() { } }
                class Derived : Base { private new void {|#0:M|}() { } }
                """,
            CompilerDiagnostics = Microsoft.CodeAnalysis.Testing.CompilerDiagnostics.None,
        };
        test.ExpectedDiagnostics.Add(Verify.Diagnostic().WithLocation(0).WithArguments("M", expected, "private", "Base"));
        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies a <c>new private</c> method hiding a <c>public</c> base method is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task PrivateMethodHidingPublicIsReportedAsync() =>
        Verify.VerifyAnalyzerAsync(
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task InternalMethodHidingPublicIsReportedAsync() =>
        Verify.VerifyAnalyzerAsync(
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task ProtectedMethodHidingPublicIsReportedAsync() =>
        Verify.VerifyAnalyzerAsync(
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task PrivateMethodHidingProtectedIsReportedAsync() =>
        Verify.VerifyAnalyzerAsync(
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task PrivatePropertyHidingPublicIsReportedAsync() =>
        Verify.VerifyAnalyzerAsync(
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task PrivateFieldHidingPublicIsReportedAsync() =>
        Verify.VerifyAnalyzerAsync(
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task PrivateEventHidingPublicIsReportedAsync() =>
        Verify.VerifyAnalyzerAsync(
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task PrivateStaticMethodHidingPublicStaticIsReportedAsync() =>
        Verify.VerifyAnalyzerAsync(
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task NarrowingGrandparentMemberIsReportedAsync() =>
        Verify.VerifyAnalyzerAsync(
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task EqualAccessibilityIsCleanAsync() =>
        Verify.VerifyAnalyzerAsync(
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task WiderAccessibilityIsCleanAsync() =>
        Verify.VerifyAnalyzerAsync(
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task IncomparableAccessibilityIsCleanAsync() =>
        Verify.VerifyAnalyzerAsync(
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task OverrideIsCleanAsync() =>
        Verify.VerifyAnalyzerAsync(
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task DifferentSignatureHidesNothingAsync() =>
        Verify.VerifyAnalyzerAsync(
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task PrivateBaseMemberIsCleanAsync() =>
        Verify.VerifyAnalyzerAsync(
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task NarrowingWithoutNewModifierIsCleanAsync() =>
        Verify.VerifyAnalyzerAsync(
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task UnrelatedMemberIsCleanAsync() =>
        Verify.VerifyAnalyzerAsync(
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
