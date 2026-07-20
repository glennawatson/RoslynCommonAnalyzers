// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Testing;

using AnalyzeActivation = SecuritySharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    SecuritySharp.Analyzers.Ses1401NonConstantTypeActivationAnalyzer>;

namespace SecuritySharp.Analyzers.Tests;

/// <summary>Unit tests for SES1401 (a type resolved from non-constant data must not be instantiated or deserialized into).</summary>
public class NonConstantTypeActivationAnalyzerUnitTest
{
    /// <summary>Verifies an inline <c>Activator.CreateInstance(Type.GetType(nonConstant))</c> is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task InlineTypeGetTypeToActivatorReportedAsync()
        => await VerifyNet90Async(
            """
            using System;

            public class C
            {
                public object M(string typeName)
                {
                    return Activator.CreateInstance({|SES1401:Type.GetType(typeName)|});
                }
            }
            """);

    /// <summary>Verifies the <c>type:</c>-named argument form is still reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NamedTypeArgumentToActivatorReportedAsync()
        => await VerifyNet90Async(
            """
            using System;

            public class C
            {
                public object M(string typeName)
                {
                    return Activator.CreateInstance(nonPublic: true, type: {|SES1401:Type.GetType(typeName)|});
                }
            }
            """);

    /// <summary>Verifies a Type-taking <c>Deserialize</c> call whose type argument is <c>Type.GetType(nonConstant)</c> is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task TypeGetTypeToDeserializeReportedAsync()
        => await VerifyNet90Async(
            """
            using System;
            using System.IO;

            public sealed class Serializer
            {
                public object Deserialize(Type type, Stream stream) => new object();
            }

            public class C
            {
                public object M(string typeName, Stream stream)
                {
                    var serializer = new Serializer();
                    return serializer.Deserialize({|SES1401:Type.GetType(typeName)|}, stream);
                }
            }
            """);

    /// <summary>Verifies a <c>Deserialize</c> overload whose Type parameter is not first is still reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task TypeArgInSecondPositionToDeserializeReportedAsync()
        => await VerifyNet90Async(
            """
            using System;

            public static class Serializer
            {
                public static object Deserialize(string payload, Type type) => new object();
            }

            public class C
            {
                public object M(string payload, string typeName)
                {
                    return Serializer.Deserialize(payload, {|SES1401:Type.GetType(typeName)|});
                }
            }
            """);

    /// <summary>Verifies a constant type name is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ConstantTypeNameIsCleanAsync()
        => await VerifyNet90Async(
            """
            using System;

            public class C
            {
                public object M()
                {
                    return Activator.CreateInstance(Type.GetType("System.Text.StringBuilder"));
                }
            }
            """);

    /// <summary>Verifies a type first stored in a local is out of the inline-only scope and not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task TypeStoredInLocalIsCleanAsync()
        => await VerifyNet90Async(
            """
            using System;

            public class C
            {
                public object M(string typeName)
                {
                    var type = Type.GetType(typeName);
                    return Activator.CreateInstance(type);
                }
            }
            """);

    /// <summary>Verifies the generic <c>Activator.CreateInstance&lt;T&gt;()</c> form is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task GenericCreateInstanceIsCleanAsync()
        => await VerifyNet90Async(
            """
            using System;

            public class C
            {
                public object M()
                {
                    return Activator.CreateInstance<C>();
                }
            }
            """);

    /// <summary>Verifies a <c>typeof</c> type argument is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task TypeofArgumentIsCleanAsync()
        => await VerifyNet90Async(
            """
            using System;

            public class C
            {
                public object M()
                {
                    return Activator.CreateInstance(typeof(C));
                }
            }
            """);

    /// <summary>Verifies an instance <c>Assembly.GetType(nonConstant)</c> (not the static <c>Type.GetType</c>) is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task AssemblyGetTypeIsCleanAsync()
        => await VerifyNet90Async(
            """
            using System;
            using System.Reflection;

            public class C
            {
                public object M(Assembly assembly, string typeName)
                {
                    return Activator.CreateInstance(assembly.GetType(typeName));
                }
            }
            """);

    /// <summary>Verifies a <c>CreateInstance</c> method that is not on <c>System.Activator</c> is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NonActivatorCreateInstanceIsCleanAsync()
        => await VerifyNet90Async(
            """
            using System;

            public sealed class Factory
            {
                public object CreateInstance(Type type) => new object();
            }

            public class C
            {
                public object M(string typeName)
                {
                    var factory = new Factory();
                    return factory.CreateInstance(Type.GetType(typeName));
                }
            }
            """);

    /// <summary>Verifies an unrelated method taking <c>Type.GetType(nonConstant)</c> is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task UnrelatedMethodIsCleanAsync()
        => await VerifyNet90Async(
            """
            using System;

            public sealed class Registry
            {
                public void Register(Type type)
                {
                }
            }

            public class C
            {
                public void M(string typeName)
                {
                    var registry = new Registry();
                    registry.Register(Type.GetType(typeName));
                }
            }
            """);

    /// <summary>Runs an analyzer-only verification against the .NET 9 reference assemblies.</summary>
    /// <param name="source">The source with diagnostic markup.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task VerifyNet90Async(string source)
    {
        var test = new AnalyzeActivation.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = source
        };

        await test.RunAsync(CancellationToken.None);
    }
}
