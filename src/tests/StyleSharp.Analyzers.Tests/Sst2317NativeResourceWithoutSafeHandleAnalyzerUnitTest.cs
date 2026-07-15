// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyNativeResource = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    StyleSharp.Analyzers.Sst2317NativeResourceWithoutSafeHandleAnalyzer>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST2317 (a disposable owning a raw native handle with no finalizer).</summary>
public class Sst2317NativeResourceWithoutSafeHandleAnalyzerUnitTest
{
    /// <summary>Verifies an owned IntPtr released in Dispose, with no finalizer, is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task OwnedNativeHandleWithoutFinalizerReportedAsync()
        => await VerifyNativeResource.VerifyAnalyzerAsync(
            """
            using System;
            using System.Runtime.InteropServices;

            public sealed class C : IDisposable
            {
                private IntPtr {|SST2317:_h|} = Marshal.AllocHGlobal(8);

                public void Dispose() => Marshal.FreeHGlobal(_h);
            }
            """);

    /// <summary>Verifies a type that already has a finalizer is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NativeHandleWithFinalizerIsCleanAsync()
        => await VerifyNativeResource.VerifyAnalyzerAsync(
            """
            using System;
            using System.Runtime.InteropServices;

            public sealed class C : IDisposable
            {
                private IntPtr _h = Marshal.AllocHGlobal(8);

                public void Dispose() => Marshal.FreeHGlobal(_h);

                ~C() => Marshal.FreeHGlobal(_h);
            }
            """);

    /// <summary>Verifies an IntPtr that is never released is not treated as an owned resource.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task UnreleasedNativeHandleIsCleanAsync()
        => await VerifyNativeResource.VerifyAnalyzerAsync(
            """
            using System;

            public sealed class C : IDisposable
            {
                private IntPtr _cookie;

                public void Dispose()
                {
                }
            }
            """);
}
