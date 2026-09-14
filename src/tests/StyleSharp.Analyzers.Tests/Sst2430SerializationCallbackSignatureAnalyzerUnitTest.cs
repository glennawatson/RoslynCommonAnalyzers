// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Testing;

using Verify = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    StyleSharp.Analyzers.Sst2430SerializationCallbackSignatureAnalyzer>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST2430 (a serialization callback with the wrong signature).</summary>
public class Sst2430SerializationCallbackSignatureAnalyzerUnitTest
{
    /// <summary>Verifies every qualified callback name is recognized, including its attribute suffix.</summary>
    /// <param name="name">The qualified callback attribute's simple name.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("OnSerializing")]
    [Arguments("OnSerializingAttribute")]
    [Arguments("OnSerialized")]
    [Arguments("OnSerializedAttribute")]
    [Arguments("OnDeserializing")]
    [Arguments("OnDeserializingAttribute")]
    [Arguments("OnDeserialized")]
    [Arguments("OnDeserializedAttribute")]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task QualifiedCallbackNamesAreRecognizedAsync(string name) =>
        VerifyOnNet80Async($"class C {{ [System.Obsolete, System.Runtime.Serialization.{name}] void {{|SST2430:M|}}<T>() {{ }} }}");

    /// <summary>Verifies an alias-qualified callback is checked after unrelated attributes.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task AliasQualifiedCallbackIsRecognizedAsync() =>
        VerifyOnNet80Async("using callback = System.Runtime.Serialization; class C { [System.Obsolete, callback::OnSerialized] void {|SST2430:M|}() { } }");

    /// <summary>Verifies unrelated attributes and attribute-free partial methods are ignored.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task NonCallbackAndPartialMethodsAreCleanAsync() =>
        VerifyOnNet80Async("using System; partial class C { [Obsolete] void A() { } [System.Obsolete] void B() { } partial void M(); partial void M() { } }");

    /// <summary>Verifies callback types can be absent in a minimal target framework.</summary>
    /// <param name="declareContext">Whether the framework supplies StreamingContext without any callback attributes.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task MissingCallbackFrameworkTypesAreIgnoredAsync(bool declareContext)
    {
        const string Source = """
            namespace System { public class Object { } public class Attribute { } }
            class CustomAttribute : System.Attribute { }
            class C { [Custom] void M() { } [Custom] void N() { } }
            """;
        var context = declareContext ? "namespace System.Runtime.Serialization { public struct StreamingContext { } }" : string.Empty;
        var tree = CSharpSyntaxTree.ParseText($"{Source}{context}");
        var compilation = CSharpCompilation.Create(nameof(Test), [tree]);
        var diagnostics = await compilation.WithAnalyzers([new Sst2430SerializationCallbackSignatureAnalyzer()]).GetAnalyzerDiagnosticsAsync();
        await Assert.That(diagnostics).IsEmpty();
    }

    /// <summary>Verifies a callback whose single parameter is not a StreamingContext is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task WrongParameterTypeIsReportedAsync() =>
        VerifyOnNet80Async(
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task StaticCallbackIsReportedAsync() =>
        VerifyOnNet80Async(
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task NoParameterIsReportedAsync() =>
        VerifyOnNet80Async(
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task NonVoidCallbackIsReportedAsync() =>
        VerifyOnNet80Async(
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task CorrectSignatureIsCleanAsync() =>
        VerifyOnNet80Async(
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task MethodWithoutCallbackAttributeIsCleanAsync() =>
        VerifyOnNet80Async(
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
        var test = new Verify.Test { ReferenceAssemblies = ReferenceAssemblies.Net.Net80, TestCode = source, };

        await test.RunAsync(CancellationToken.None);
    }
}
