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

    /// <summary>Verifies Fix All prefixes every reported property summary in one pass (SST1623).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task FixAllRewritesEveryOccurrenceAsync()
    {
        const string Source = """
            /// <summary>A container.</summary>
            public class C
            {
                /// {|SST1623:<summary>The count.</summary>|}
                public int Count { get; set; }

                /// {|SST1623:<summary>The name.</summary>|}
                public string Name { get; set; }

                /// {|SST1623:<summary>The flag.</summary>|}
                public bool Flag { get; set; }
            }
            """;
        const string FixedSource = """
            /// <summary>A container.</summary>
            public class C
            {
                /// <summary>Gets or sets the count.</summary>
                public int Count { get; set; }

                /// <summary>Gets or sets the name.</summary>
                public string Name { get; set; }

                /// <summary>Gets or sets the flag.</summary>
                public bool Flag { get; set; }
            }
            """;

        await VerifyProperty.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies an init-only property summary should begin with "Gets", not "Gets or sets".</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task InitOnlyPropertyExpectsGetsAsync()
    {
        const string Source = """
            /// <summary>A container.</summary>
            public class C
            {
                /// <summary>Gets the count.</summary>
                public int Count { get; init; }
            }

            namespace System.Runtime.CompilerServices
            {
                /// <summary>Reserved for compiler use to enable init-only setters on netstandard.</summary>
                internal static class IsExternalInit { }
            }
            """;

        await VerifyProperty.VerifyAnalyzerAsync(Source);
    }

    /// <summary>Verifies the fix prefixes an init-only property summary with "Gets".</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task InitOnlyPropertyFixedWithGetsAsync()
    {
        const string Source = """
            /// <summary>A container.</summary>
            public class C
            {
                /// {|SST1623:<summary>The count.</summary>|}
                public int Count { get; init; }
            }

            namespace System.Runtime.CompilerServices
            {
                /// <summary>Reserved for compiler use to enable init-only setters on netstandard.</summary>
                internal static class IsExternalInit { }
            }
            """;
        const string FixedSource = """
            /// <summary>A container.</summary>
            public class C
            {
                /// <summary>Gets the count.</summary>
                public int Count { get; init; }
            }

            namespace System.Runtime.CompilerServices
            {
                /// <summary>Reserved for compiler use to enable init-only setters on netstandard.</summary>
                internal static class IsExternalInit { }
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

    /// <summary>Verifies Fix All rewrites every reported constructor summary to the standard text in one pass (SST1642).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ConstructorFixAllRewritesEveryOccurrenceAsync()
    {
        const string Source = """
            /// <summary>A.</summary>
            public class A
            {
                /// {|SST1642:<summary>Creates an A.</summary>|}
                public A() { }
            }

            /// <summary>B.</summary>
            public class B
            {
                /// {|SST1642:<summary>Creates a B.</summary>|}
                public B() { }
            }

            /// <summary>D.</summary>
            public class D
            {
                /// {|SST1642:<summary>Creates a D.</summary>|}
                public D() { }
            }
            """;
        const string FixedSource = """
            /// <summary>A.</summary>
            public class A
            {
                /// <summary>Initializes a new instance of the <see cref="A"/> class.</summary>
                public A() { }
            }

            /// <summary>B.</summary>
            public class B
            {
                /// <summary>Initializes a new instance of the <see cref="B"/> class.</summary>
                public B() { }
            }

            /// <summary>D.</summary>
            public class D
            {
                /// <summary>Initializes a new instance of the <see cref="D"/> class.</summary>
                public D() { }
            }
            """;

        await VerifyConstructor.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a private constructor using the "Prevents a default instance" wording is accepted (no SST1642).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task PrivateConstructorPreventsDefaultInstanceAsync()
    {
        const string Source = """
            /// <summary>A container.</summary>
            public class C
            {
                /// <summary>Prevents a default instance of the <see cref="C"/> class from being created.</summary>
                private C() { }
            }
            """;

        await VerifyConstructor.VerifyAnalyzerAsync(Source);
    }

    /// <summary>Verifies a private constructor may still use the standard "Initializes a new instance" wording.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task PrivateConstructorInitializesAsync()
    {
        const string Source = """
            /// <summary>A container.</summary>
            public class C
            {
                /// <summary>Initializes a new instance of the <see cref="C"/> class.</summary>
                private C() { }
            }
            """;

        await VerifyConstructor.VerifyAnalyzerAsync(Source);
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

    /// <summary>Verifies Fix All rewrites every reported destructor summary to the standard text in one pass (SST1643).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task DestructorFixAllRewritesEveryOccurrenceAsync()
    {
        const string Source = """
            /// <summary>A first container.</summary>
            public class C
            {
                /// {|SST1643:<summary>Cleans up.</summary>|}
                ~C() { }
            }

            /// <summary>A second container.</summary>
            public class D
            {
                /// {|SST1643:<summary>Tears down.</summary>|}
                ~D() { }
            }

            /// <summary>A third container.</summary>
            public class E
            {
                /// {|SST1643:<summary>Disposes resources.</summary>|}
                ~E() { }
            }
            """;
        const string FixedSource = """
            /// <summary>A first container.</summary>
            public class C
            {
                /// <summary>Finalizes an instance of the <see cref="C"/> class.</summary>
                ~C() { }
            }

            /// <summary>A second container.</summary>
            public class D
            {
                /// <summary>Finalizes an instance of the <see cref="D"/> class.</summary>
                ~D() { }
            }

            /// <summary>A third container.</summary>
            public class E
            {
                /// <summary>Finalizes an instance of the <see cref="E"/> class.</summary>
                ~E() { }
            }
            """;

        await VerifyDestructor.VerifyCodeFixAsync(Source, FixedSource);
    }
}
