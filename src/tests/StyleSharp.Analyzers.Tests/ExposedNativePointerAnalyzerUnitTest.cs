// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Verify = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<StyleSharp.Analyzers.Sst2328ExposedNativePointerAnalyzer>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST2328 (a visible field or property exposing a raw native pointer).</summary>
public class ExposedNativePointerAnalyzerUnitTest
{
    /// <summary>Verifies a public <c>IntPtr</c> instance field is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task PublicIntPtrFieldIsReportedAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            using System;

            public class NativeOwner
            {
                public IntPtr {|SST2328:Handle|};
            }
            """);

    /// <summary>Verifies a public <c>UIntPtr</c> instance field is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task PublicUIntPtrFieldIsReportedAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            using System;

            public class NativeOwner
            {
                public UIntPtr {|SST2328:Region|};
            }
            """);

    /// <summary>Verifies a public <c>nint</c> instance field is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task PublicNintFieldIsReportedAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            public class NativeOwner
            {
                public nint {|SST2328:Handle|};
            }
            """);

    /// <summary>Verifies a public <c>nuint</c> instance field is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task PublicNuintFieldIsReportedAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            public class NativeOwner
            {
                public nuint {|SST2328:Region|};
            }
            """);

    /// <summary>Verifies a protected <c>IntPtr</c> field is reported, its handle being reachable by a derived type.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ProtectedIntPtrFieldIsReportedAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            using System;

            public class NativeOwner
            {
                protected IntPtr {|SST2328:Handle|};
            }
            """);

    /// <summary>Verifies a protected internal <c>IntPtr</c> field is reported, its handle reaching outside the assembly.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ProtectedInternalIntPtrFieldIsReportedAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            using System;

            public class NativeOwner
            {
                protected internal IntPtr {|SST2328:Handle|};
            }
            """);

    /// <summary>Verifies every declarator of a multi-declarator native field is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task EveryDeclaratorOfMultiNativeFieldIsReportedAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            using System;

            public class NativeOwner
            {
                public IntPtr {|SST2328:First|}, {|SST2328:Second|};
            }
            """);

    /// <summary>Verifies a public read/write <c>IntPtr</c> property is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task PublicReadWriteIntPtrPropertyIsReportedAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            using System;

            public class NativeOwner
            {
                public IntPtr {|SST2328:Handle|} { get; set; }
            }
            """);

    /// <summary>Verifies a public get-only <c>IntPtr</c> property is reported: reading the pointer alone is enough to corrupt it.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task PublicGetOnlyIntPtrPropertyIsReportedAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            using System;

            public class NativeOwner
            {
                public IntPtr {|SST2328:Handle|} { get; }
            }
            """);

    /// <summary>Verifies a public <c>IntPtr</c> property with a private setter is still reported on its readable half.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task PublicIntPtrPropertyWithPrivateSetterIsReportedAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            using System;

            public class NativeOwner
            {
                public IntPtr {|SST2328:Handle|} { get; private set; }
            }
            """);

    /// <summary>Verifies a public <c>IntPtr</c> field on a struct is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task PublicIntPtrFieldOnStructIsReportedAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            using System;

            public struct NativeRegion
            {
                public IntPtr {|SST2328:Handle|};
            }
            """);

    /// <summary>Verifies a positional record parameter of native pointer type, surfaced as a public property, is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task PositionalRecordNativePointerParameterIsReportedAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            using System;

            public record struct NativeRegion(IntPtr {|SST2328:Handle|});
            """);

    /// <summary>Verifies a public virtual native property is reported, while its override is left alone.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task OverridingNativePropertyIsNotReportedAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            using System;

            public abstract class NativeBase
            {
                public virtual IntPtr {|SST2328:Handle|} { get; set; }
            }

            public sealed class NativeDerived : NativeBase
            {
                public override IntPtr Handle { get; set; }
            }
            """);

    /// <summary>Verifies a private native field is not reported, the handle staying under the type's control.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task PrivateIntPtrFieldIsNotReportedAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            using System;

            public class NativeOwner
            {
                private IntPtr _handle;

                public void Use() => GC.KeepAlive(_handle);
            }
            """);

    /// <summary>Verifies an internal native field is not reported, the handle staying inside the assembly.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task InternalIntPtrFieldIsNotReportedAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            using System;

            public class NativeOwner
            {
                internal IntPtr Handle;
            }
            """);

    /// <summary>Verifies a private protected native field is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task PrivateProtectedIntPtrFieldIsNotReportedAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            using System;

            public class NativeOwner
            {
                private protected IntPtr Handle;
            }
            """);

    /// <summary>Verifies a static native field is not reported: it is not part of an instance's surface.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task StaticIntPtrFieldIsNotReportedAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            using System;

            public class NativeOwner
            {
                public static IntPtr Shared;
            }
            """);

    /// <summary>Verifies a visible field of a non-pointer type is not reported, distinguishing this from a general visible-field rule.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task PublicNonPointerFieldIsNotReportedAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            public class Data
            {
                public int Value;
            }
            """);

    /// <summary>Verifies a visible property of a non-pointer type is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task PublicNonPointerPropertyIsNotReportedAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            public class Data
            {
                public int Value { get; set; }
            }
            """);

    /// <summary>Verifies a native-pointer indexer is not reported: it hands back a computed value, not a stored handle.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NativePointerIndexerIsNotReportedAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            using System;

            public class NativeTable
            {
                public IntPtr this[int index] => IntPtr.Zero;
            }
            """);

    /// <summary>Verifies an explicitly-implemented interface native property is not reported: it is private and reachable only through the interface.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ExplicitInterfaceNativePropertyIsNotReportedAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            using System;

            public interface INativeOwner
            {
                IntPtr Handle { get; }
            }

            public sealed class NativeOwner : INativeOwner
            {
                IntPtr INativeOwner.Handle => IntPtr.Zero;
            }
            """);

    /// <summary>Verifies an interface declaring a native property is not reported: an interface owns no memory to hide.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task InterfaceNativePropertyIsNotReportedAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            using System;

            public interface INativeOwner
            {
                IntPtr Handle { get; set; }
            }
            """);

    /// <summary>Verifies a static class with a public native field is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task StaticClassNativeFieldIsNotReportedAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            using System;

            public static class NativeConstants
            {
                public static IntPtr Sentinel;
            }
            """);
}
