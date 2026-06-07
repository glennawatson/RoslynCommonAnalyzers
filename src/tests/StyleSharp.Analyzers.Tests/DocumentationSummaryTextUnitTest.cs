// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyConstructor = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.MemberDocumentationAnalyzer,
    StyleSharp.Analyzers.ConstructorSummaryCodeFixProvider>;
using VerifyDestructor = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.MemberDocumentationAnalyzer,
    StyleSharp.Analyzers.DestructorSummaryCodeFixProvider>;
using VerifyProperty = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.MemberDocumentationAnalyzer,
    StyleSharp.Analyzers.PropertySummaryCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for the summary-text documentation rules (SST1623 property accessors, SST1642 constructor text).</summary>
public class DocumentationSummaryTextUnitTest
{
    /// <summary>Verifies a property summary already starting with "Gets or sets" is accepted.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ValidPropertyAsync()
    {
        const string Source = """
            /// <summary>A container.</summary>
            public class C
            {
                /// <summary>Gets or sets the count.</summary>
                public int Count { get; set; }
            }
            """;

        await VerifyProperty.VerifyAnalyzerAsync(Source);
    }

    /// <summary>Verifies a property summary is reported and prefixed with the accessor phrase (SST1623).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task PropertyAccessorsAsync()
    {
        const string Source = """
            /// <summary>A container.</summary>
            public class C
            {
                /// {|SST1623:<summary>The count.</summary>|}
                public int Count { get; set; }
            }
            """;
        const string FixedSource = """
            /// <summary>A container.</summary>
            public class C
            {
                /// <summary>Gets or sets the count.</summary>
                public int Count { get; set; }
            }
            """;

        await VerifyProperty.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a constructor summary with the standard text is accepted.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ValidConstructorAsync()
    {
        const string Source = """
            /// <summary>A container.</summary>
            public class C
            {
                /// <summary>Initializes a new instance of the <see cref="C"/> class.</summary>
                public C() { }
            }
            """;

        await VerifyConstructor.VerifyAnalyzerAsync(Source);
    }

    /// <summary>Verifies a constructor summary is reported and rewritten to the standard text (SST1642).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ConstructorStandardTextAsync()
    {
        const string Source = """
            /// <summary>A container.</summary>
            public class C
            {
                /// {|SST1642:<summary>Creates the thing.</summary>|}
                public C() { }
            }
            """;
        const string FixedSource = """
            /// <summary>A container.</summary>
            public class C
            {
                /// <summary>Initializes a new instance of the <see cref="C"/> class.</summary>
                public C() { }
            }
            """;

        await VerifyConstructor.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a constructor-style destructor summary is accepted.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ValidDestructorAsync()
    {
        const string Source = """
            /// <summary>A container.</summary>
            public class C
            {
                /// <summary>Finalizes an instance of the <see cref="C"/> class.</summary>
                ~C() { }
            }
            """;

        await VerifyDestructor.VerifyAnalyzerAsync(Source);
    }

    /// <summary>Verifies a destructor summary is reported and rewritten to the standard text (SST1643).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task DestructorStandardTextAsync()
    {
        const string Source = """
            /// <summary>A container.</summary>
            public class C
            {
                /// {|SST1643:<summary>Cleans up.</summary>|}
                ~C() { }
            }
            """;
        const string FixedSource = """
            /// <summary>A container.</summary>
            public class C
            {
                /// <summary>Finalizes an instance of the <see cref="C"/> class.</summary>
                ~C() { }
            }
            """;

        await VerifyDestructor.VerifyCodeFixAsync(Source, FixedSource);
    }
}
