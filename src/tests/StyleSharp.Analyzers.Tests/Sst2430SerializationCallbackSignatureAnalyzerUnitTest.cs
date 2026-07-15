// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Testing;

using Verify = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    StyleSharp.Analyzers.Sst2430SerializationCallbackSignatureAnalyzer>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST2430 (a serialization callback with the wrong signature).</summary>
public class Sst2430SerializationCallbackSignatureAnalyzerUnitTest
{
    /// <summary>Verifies a callback whose single parameter is not a StreamingContext is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task WrongParameterTypeIsReportedAsync()
        => await VerifyOnNet80Async(
            """
            using System.Runtime.Serialization;

            public class C
            {
                [OnDeserialized]
                private void {|SST2430:M|}(int x)
                {
                }
            }
            """);

    /// <summary>Verifies a static callback is reported: the serializer only invokes instance methods.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task StaticCallbackIsReportedAsync()
        => await VerifyOnNet80Async(
            """
            using System.Runtime.Serialization;

            public class C
            {
                [OnSerializing]
                public static void {|SST2430:M|}(StreamingContext c)
                {
                }
            }
            """);

    /// <summary>Verifies a callback with no parameter is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NoParameterIsReportedAsync()
        => await VerifyOnNet80Async(
            """
            using System.Runtime.Serialization;

            public class C
            {
                [OnDeserialized]
                private void {|SST2430:M|}()
                {
                }
            }
            """);

    /// <summary>Verifies a non-void callback is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NonVoidCallbackIsReportedAsync()
        => await VerifyOnNet80Async(
            """
            using System.Runtime.Serialization;

            public class C
            {
                [OnDeserialized]
                private int {|SST2430:M|}(StreamingContext c) => 0;
            }
            """);

    /// <summary>Verifies a correctly shaped callback is left alone.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task CorrectSignatureIsCleanAsync()
        => await VerifyOnNet80Async(
            """
            using System.Runtime.Serialization;

            public class C
            {
                [OnDeserialized]
                private void M(StreamingContext context)
                {
                }
            }
            """);

    /// <summary>Verifies a method with no serialization attribute is not measured.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task MethodWithoutCallbackAttributeIsCleanAsync()
        => await VerifyOnNet80Async(
            """
            public class C
            {
                private void M(int x)
                {
                }
            }
            """);

    /// <summary>Runs a source against reference assemblies whose serialization callbacks are present.</summary>
    /// <param name="source">The test source, with markup.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task VerifyOnNet80Async(string source)
    {
        var test = new Verify.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
            TestCode = source,
        };

        await test.RunAsync(CancellationToken.None);
    }
}
