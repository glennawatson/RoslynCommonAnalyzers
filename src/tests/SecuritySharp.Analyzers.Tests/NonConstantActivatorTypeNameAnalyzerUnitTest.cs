// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Testing;

using AnalyzeActivatorTypeName = SecuritySharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    SecuritySharp.Analyzers.Ses1404NonConstantActivatorTypeNameAnalyzer>;

namespace SecuritySharp.Analyzers.Tests;

/// <summary>Unit tests for SES1404 (a type must not be instantiated by name from non-constant data).</summary>
public class NonConstantActivatorTypeNameAnalyzerUnitTest
{
    /// <summary>Verifies the two-string <c>Activator.CreateInstance(assemblyName, typeName)</c> overload with a non-constant type name is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task CreateInstanceStringTypeNameReportedAsync()
        => await VerifyNet90Async(
            """
            using System;

            public class C
            {
                public void M(string typeName)
                {
                    Activator.CreateInstance("MyAssembly", {|SES1404:typeName|});
                }
            }
            """);

    /// <summary>Verifies the <c>(assemblyName, typeName, activationAttributes)</c> overload with a non-constant type name is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task CreateInstanceWithActivationAttributesReportedAsync()
        => await VerifyNet90Async(
            """
            using System;

            public class C
            {
                public void M(string typeName, object[] attributes)
                {
                    Activator.CreateInstance("MyAssembly", {|SES1404:typeName|}, attributes);
                }
            }
            """);

    /// <summary>Verifies the long binding-flags overload with a non-constant type name is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task CreateInstanceBindingFlagsOverloadReportedAsync()
        => await VerifyNet90Async(
            """
            using System;
            using System.Globalization;
            using System.Reflection;

            public class C
            {
                public void M(string typeName)
                {
                    Activator.CreateInstance(
                        "MyAssembly",
                        {|SES1404:typeName|},
                        false,
                        BindingFlags.Public | BindingFlags.Instance,
                        null,
                        null,
                        CultureInfo.InvariantCulture,
                        null);
                }
            }
            """);

    /// <summary>Verifies <c>Activator.CreateInstanceFrom(assemblyFile, typeName)</c> with a non-constant type name is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task CreateInstanceFromReportedAsync()
        => await VerifyNet90Async(
            """
            using System;

            public class C
            {
                public void M(string typeName)
                {
                    Activator.CreateInstanceFrom("my.dll", {|SES1404:typeName|});
                }
            }
            """);

    /// <summary>Verifies the <c>typeName:</c>-named argument form is still reported when reordered.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NamedTypeNameArgumentReportedAsync()
        => await VerifyNet90Async(
            """
            using System;

            public class C
            {
                public void M(string typeName)
                {
                    Activator.CreateInstance(typeName: {|SES1404:typeName|}, assemblyName: "MyAssembly");
                }
            }
            """);

    /// <summary>Verifies a type name built by concatenating non-constant data is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ConcatenatedTypeNameReportedAsync()
        => await VerifyNet90Async(
            """
            using System;

            public class C
            {
                public void M(string suffix)
                {
                    Activator.CreateInstance("MyAssembly", {|SES1404:"MyApp.Handlers." + suffix|});
                }
            }
            """);

    /// <summary>Verifies a constant literal type name is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ConstantTypeNameIsCleanAsync()
        => await VerifyNet90Async(
            """
            using System;

            public class C
            {
                public void M()
                {
                    Activator.CreateInstance("MyAssembly", "MyApp.Handlers.KnownHandler");
                }
            }
            """);

    /// <summary>Verifies a type name held in a <c>const</c> local (constant-folded) is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ConstLocalTypeNameIsCleanAsync()
        => await VerifyNet90Async(
            """
            using System;

            public class C
            {
                public void M()
                {
                    const string typeName = "MyApp.Handlers.KnownHandler";
                    Activator.CreateInstance("MyAssembly", typeName);
                }
            }
            """);

    /// <summary>Verifies the Type-taking overload (the SES1401 inline shape) is not double-reported here.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task TypeTakingOverloadIsCleanAsync()
        => await VerifyNet90Async(
            """
            using System;

            public class C
            {
                public void M(string typeName)
                {
                    Activator.CreateInstance(Type.GetType(typeName), new object[0]);
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

    /// <summary>Verifies a two-string <c>CreateInstance</c> method that is not on <c>System.Activator</c> is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NonActivatorCreateInstanceIsCleanAsync()
        => await VerifyNet90Async(
            """
            public sealed class Factory
            {
                public object CreateInstance(string assemblyName, string typeName) => new object();
            }

            public class C
            {
                public void M(string typeName)
                {
                    var factory = new Factory();
                    factory.CreateInstance("MyAssembly", typeName);
                }
            }
            """);

    /// <summary>Runs an analyzer-only verification against the .NET 9 reference assemblies.</summary>
    /// <param name="source">The source with diagnostic markup.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task VerifyNet90Async(string source)
    {
        var test = new AnalyzeActivatorTypeName.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = source
        };

        await test.RunAsync(CancellationToken.None);
    }
}
