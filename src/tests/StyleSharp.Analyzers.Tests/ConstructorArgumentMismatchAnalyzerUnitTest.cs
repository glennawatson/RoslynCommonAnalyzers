// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Verify = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    StyleSharp.Analyzers.Sst2487ConstructorArgumentMismatchAnalyzer>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST2487 (a [ConstructorArgument] name that matches no constructor parameter).</summary>
public class ConstructorArgumentMismatchAnalyzerUnitTest
{
    /// <summary>
    /// The markup attribute, stubbed under its real namespace. The reference set has no WPF assembly, and the
    /// analyzer resolves the attribute by metadata name, so a source declaration is what it binds against. It
    /// is appended after each body so the body's own <c>using</c> directives stay at the top of the file.
    /// </summary>
    private const string MarkupAttributeStub = """


        namespace System.Windows.Markup
        {
            [System.AttributeUsage(System.AttributeTargets.Property)]
            public sealed class ConstructorArgumentAttribute : System.Attribute
            {
                public ConstructorArgumentAttribute(string argumentName) => ArgumentName = argumentName;

                public string ArgumentName { get; }
            }
        }
        """;

    /// <summary>Verifies a name matching no constructor parameter is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task MismatchIsReportedAsync()
        => await VerifyWithStubAsync(
            """
            using System.Windows.Markup;

            public class MapExtension
            {
                public MapExtension(string source) => Source = source;

                [ConstructorArgument({|SST2487:"src"|})]
                public string Source { get; set; }
            }
            """);

    /// <summary>Verifies a name matching the constructor parameter is left alone.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task MatchingNameIsCleanAsync()
        => await VerifyWithStubAsync(
            """
            using System.Windows.Markup;

            public class MapExtension
            {
                public MapExtension(string source) => Source = source;

                [ConstructorArgumentAttribute("source")]
                public string Source { get; set; }
            }
            """);

    /// <summary>Verifies a name matching a parameter of any constructor, not only the first, is clean.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task MatchInAnyConstructorIsCleanAsync()
        => await VerifyWithStubAsync(
            """
            using System.Windows.Markup;

            public class MapExtension
            {
                public MapExtension(string source) => Source = source;

                public MapExtension(string source, string fallback) => Source = fallback ?? source;

                [ConstructorArgument("fallback")]
                public string Source { get; set; }
            }
            """);

    /// <summary>Verifies a type whose only constructor takes no such parameter is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task TypeWithoutMatchingParameterIsReportedAsync()
        => await VerifyWithStubAsync(
            """
            using System.Windows.Markup;

            public class MapExtension
            {
                [ConstructorArgument({|SST2487:"source"|})]
                public string Source { get; set; }
            }
            """);

    /// <summary>Verifies a fully qualified attribute name is still measured and reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task QualifiedAttributeNameIsReportedAsync()
        => await VerifyWithStubAsync(
            """
            public class MapExtension
            {
                public MapExtension(string source) => Source = source;

                [System.Windows.Markup.ConstructorArgument({|SST2487:"src"|})]
                public string Source { get; set; }
            }
            """);

    /// <summary>Verifies an argument that is not a string literal is not measured.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NonLiteralArgumentIsCleanAsync()
        => await VerifyWithStubAsync(
            """
            using System.Windows.Markup;

            public class MapExtension
            {
                private const string Name = "src";

                public MapExtension(string source) => Source = source;

                [ConstructorArgument(Name)]
                public string Source { get; set; }
            }
            """);

    /// <summary>Verifies an argument that is a literal but not a string is not measured.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NonStringLiteralArgumentIsCleanAsync()
        => await VerifyWithStubAsync(
            """
            using System.Windows.Markup;

            public class MapExtension
            {
                public MapExtension(string source) => Source = source;

                [ConstructorArgument(null)]
                public string Source { get; set; }
            }
            """);

    /// <summary>Verifies an attribute written with no argument is not measured.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task EmptyArgumentListIsCleanAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            namespace System.Windows.Markup
            {
                [System.AttributeUsage(System.AttributeTargets.Property, AllowMultiple = true)]
                public sealed class ConstructorArgumentAttribute : System.Attribute
                {
                    public ConstructorArgumentAttribute()
                    {
                    }

                    public ConstructorArgumentAttribute(string argumentName) => ArgumentName = argumentName;

                    public string ArgumentName { get; }
                }
            }

            namespace App
            {
                using System.Windows.Markup;

                public class MapExtension
                {
                    [ConstructorArgument]
                    [ConstructorArgument()]
                    public string Source { get; set; }
                }
            }
            """);

    /// <summary>Verifies a different attribute on the property is not measured.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task DifferentAttributeIsCleanAsync()
        => await VerifyWithStubAsync(
            """
            using System;
            using System.Windows.Markup;

            public class MapExtension
            {
                public MapExtension(string source) => Source = source;

                [Obsolete]
                public string Source { get; set; }
            }
            """);

    /// <summary>Verifies a same-named attribute from another namespace is not measured.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SameNamedAttributeFromOtherNamespaceIsCleanAsync()
        => await VerifyWithStubAsync(
            """
            namespace Other
            {
                [System.AttributeUsage(System.AttributeTargets.Property)]
                public sealed class ConstructorArgumentAttribute : System.Attribute
                {
                    public ConstructorArgumentAttribute(string argumentName) => ArgumentName = argumentName;

                    public string ArgumentName { get; }
                }
            }

            public class MapExtension
            {
                public MapExtension(string source) => Source = source;

                [Other.ConstructorArgument("nope")]
                public string Source { get; set; }
            }
            """);

    /// <summary>Verifies nothing is registered when the markup attribute is absent from the compilation.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task WithoutMarkupAttributeNothingIsReportedAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            namespace Other
            {
                [System.AttributeUsage(System.AttributeTargets.Property)]
                public sealed class ConstructorArgumentAttribute : System.Attribute
                {
                    public ConstructorArgumentAttribute(string argumentName) => ArgumentName = argumentName;

                    public string ArgumentName { get; }
                }
            }

            namespace App
            {
                using Other;

                public class MapExtension
                {
                    public MapExtension(string source) => Source = source;

                    [ConstructorArgument("nope")]
                    public string Source { get; set; }
                }
            }
            """);

    /// <summary>Verifies the attribute on a member other than a property is not measured.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task AttributeNotOnPropertyIsCleanAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            namespace System.Windows.Markup
            {
                [System.AttributeUsage(System.AttributeTargets.All)]
                public sealed class ConstructorArgumentAttribute : System.Attribute
                {
                    public ConstructorArgumentAttribute(string argumentName) => ArgumentName = argumentName;

                    public string ArgumentName { get; }
                }
            }

            namespace App
            {
                using System.Windows.Markup;

                public class MapExtension
                {
                    public MapExtension(string source) => Source = source;

                    public string Source { get; set; }

                    [ConstructorArgument("nope")]
                    public void Configure()
                    {
                    }
                }
            }
            """);

    /// <summary>Runs a source with the markup attribute stub appended.</summary>
    /// <param name="body">The test body, with markup.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task VerifyWithStubAsync(string body)
        => await Verify.VerifyAnalyzerAsync(body + MarkupAttributeStub);
}
