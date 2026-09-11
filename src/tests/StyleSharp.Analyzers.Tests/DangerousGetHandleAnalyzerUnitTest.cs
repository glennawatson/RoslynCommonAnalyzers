// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using Verify = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    StyleSharp.Analyzers.Sst2484DangerousGetHandleAnalyzer>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Tests for SST2484 (a raw handle read through a safe handle's dangerous accessor).</summary>
public class DangerousGetHandleAnalyzerUnitTest
{
    /// <summary>A minimal concrete safe handle the samples call through.</summary>
    private const string SafeHandleType = """
                                          public sealed class MyHandle : SafeHandle
                                          {
                                              public MyHandle() : base(IntPtr.Zero, true) { }
                                              public override bool IsInvalid => handle == IntPtr.Zero;
                                              protected override bool ReleaseHandle() => true;
                                          }
                                          """;

    /// <summary>Verifies a member-access call to the accessor on a safe handle is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task MemberAccessOnSafeHandleIsReportedAsync() =>
        Verify.VerifyAnalyzerAsync(
            $$"""
            using System;
            using System.Runtime.InteropServices;

            {{SafeHandleType}}

            public class C
            {
                public IntPtr M(MyHandle h) => {|SST2484:h.DangerousGetHandle()|};
            }
            """);

    /// <summary>Verifies a call through the abstract base safe handle type is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task CallThroughBaseSafeHandleIsReportedAsync() =>
        Verify.VerifyAnalyzerAsync(
            """
            using System;
            using System.Runtime.InteropServices;

            public class C
            {
                public IntPtr M(SafeHandle h) => {|SST2484:h.DangerousGetHandle()|};
            }
            """);

    /// <summary>Verifies a null-conditional call to the accessor is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task ConditionalAccessIsReportedAsync() =>
        Verify.VerifyAnalyzerAsync(
            $$"""
            using System;
            using System.Runtime.InteropServices;

            {{SafeHandleType}}

            public class C
            {
                public IntPtr? M(MyHandle h) => h?{|SST2484:.DangerousGetHandle()|};
            }
            """);

    /// <summary>Verifies an unqualified call to the inherited accessor from within a derived handle is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task UnqualifiedInheritedCallIsReportedAsync() =>
        Verify.VerifyAnalyzerAsync(
            """
            using System;
            using System.Runtime.InteropServices;

            public sealed class MyHandle : SafeHandle
            {
                public MyHandle() : base(IntPtr.Zero, true) { }
                public override bool IsInvalid => handle == IntPtr.Zero;
                protected override bool ReleaseHandle() => true;

                public IntPtr Raw() => {|SST2484:DangerousGetHandle()|};
            }
            """);

    /// <summary>Verifies a <c>this</c>-qualified call to the inherited accessor is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task ThisQualifiedInheritedCallIsReportedAsync() =>
        Verify.VerifyAnalyzerAsync(
            """
            using System;
            using System.Runtime.InteropServices;

            public sealed class MyHandle : SafeHandle
            {
                public MyHandle() : base(IntPtr.Zero, true) { }
                public override bool IsInvalid => handle == IntPtr.Zero;
                protected override bool ReleaseHandle() => true;

                public IntPtr Raw() => {|SST2484:this.DangerousGetHandle()|};
            }
            """);

    /// <summary>Verifies a same-named method on an unrelated type is never reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task SameNamedMethodOnUnrelatedTypeIsCleanAsync() =>
        Verify.VerifyAnalyzerAsync(
            """
            using System;

            public sealed class NotAHandle
            {
                public IntPtr DangerousGetHandle() => IntPtr.Zero;
            }

            public class C
            {
                public IntPtr M(NotAHandle n) => n.DangerousGetHandle();
            }
            """);

    /// <summary>Verifies a same-named method that takes an argument is never reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task SameNamedMethodWithArgumentIsCleanAsync() =>
        Verify.VerifyAnalyzerAsync(
            """
            using System;

            public sealed class NotAHandle
            {
                public IntPtr DangerousGetHandle(int slot) => IntPtr.Zero;
            }

            public class C
            {
                public IntPtr M(NotAHandle n) => n.DangerousGetHandle(0);
            }
            """);

    /// <summary>Verifies another method on a safe handle is never reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task OtherSafeHandleMemberIsCleanAsync() =>
        Verify.VerifyAnalyzerAsync(
            $$"""
            using System;
            using System.Runtime.InteropServices;

            {{SafeHandleType}}

            public class C
            {
                public bool M(MyHandle h) => h.IsInvalid;
            }
            """);

    /// <summary>Verifies a delegate invocation with no simple member name is never reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task DelegateInvocationIsCleanAsync() =>
        Verify.VerifyAnalyzerAsync(
            """
            using System;

            public class C
            {
                public IntPtr M(Func<Func<IntPtr>> factory) => factory()();
            }
            """);
}
