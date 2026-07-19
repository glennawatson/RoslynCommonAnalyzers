// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Verify = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<StyleSharp.Analyzers.Sst2324MemberMoreAccessibleThanContainingTypeAnalyzer>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST2324 (a member declared more accessible than its containing type).</summary>
public class Sst2324MemberMoreAccessibleThanContainingTypeAnalyzerUnitTest
{
    /// <summary>Verifies a <c>public</c> method inside an <c>internal</c> class is reported on its modifier.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task PublicMethodInInternalTypeIsReportedAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            internal class Container
            {
                {|SST2324:public|} void Method()
                {
                }
            }
            """);

    /// <summary>Verifies a <c>public</c> nested type inside an <c>internal</c> class is reported on its modifier.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task PublicNestedTypeInInternalTypeIsReportedAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            internal class Container
            {
                {|SST2324:public|} class Nested
                {
                }
            }
            """);

    /// <summary>Verifies a <c>public</c> member of a <c>public</c> type nested in an <c>internal</c> type is still reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task PublicMemberOfPublicTypeNestedInInternalTypeIsReportedAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            internal class Outer
            {
                {|SST2324:public|} class Middle
                {
                    {|SST2324:public|} void Method()
                    {
                    }
                }
            }
            """);

    /// <summary>Verifies a <c>public</c> field inside an <c>internal</c> class is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task PublicFieldInInternalTypeIsReportedAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            internal class Container
            {
                {|SST2324:public|} int Value;
            }
            """);

    /// <summary>Verifies a <c>protected internal</c> member inside an <c>internal</c> class is reported, its cross-assembly derived reach being dead.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ProtectedInternalMemberInInternalTypeIsReportedAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            internal class Container
            {
                {|SST2324:protected|} internal void Method()
                {
                }
            }
            """);

    /// <summary>Verifies a member declared exactly as accessible as its container is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task MemberMatchingContainerAccessibilityIsNotReportedAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            internal class Container
            {
                internal void Method()
                {
                }
            }
            """);

    /// <summary>Verifies a member less accessible than its container is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task MemberLessAccessibleThanContainerIsNotReportedAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            public class Container
            {
                internal void Method()
                {
                }
            }
            """);

    /// <summary>Verifies a <c>public</c> member of a <c>public</c> type is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task PublicMemberOfPublicTypeIsNotReportedAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            public class Container
            {
                public void Method()
                {
                }
            }
            """);

    /// <summary>Verifies a <c>protected</c> member inside an <c>internal</c> class is not reported: neither reach is a superset of the other.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ProtectedMemberInInternalTypeIsNotReportedAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            internal class Container
            {
                protected void Method()
                {
                }
            }
            """);

    /// <summary>Verifies an explicit interface implementation, which has no accessibility modifier, is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ExplicitInterfaceImplementationIsNotReportedAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            public interface IThing
            {
                void Do();
            }

            internal class Thing : IThing
            {
                void IThing.Do()
                {
                }
            }
            """);

    /// <summary>Verifies a <c>public</c> implicit interface implementation, whose accessibility the contract fixes, is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ImplicitInterfaceImplementationIsNotReportedAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            public interface IThing
            {
                void Do();
            }

            internal class Thing : IThing
            {
                public void Do()
                {
                }
            }
            """);

    /// <summary>Verifies a <c>public</c> override, whose accessibility its base fixes, is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task PublicOverrideInInternalTypeIsNotReportedAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            public abstract class Base
            {
                public abstract void Method();
            }

            internal class Derived : Base
            {
                public override void Method()
                {
                }
            }
            """);

    /// <summary>Verifies a <c>public</c> member of a top-level <c>public</c> type is untouched even when it has a nested internal type.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task PublicNestedTypeInPublicTypeIsNotReportedAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            public class Container
            {
                public class Nested
                {
                }
            }
            """);
}
