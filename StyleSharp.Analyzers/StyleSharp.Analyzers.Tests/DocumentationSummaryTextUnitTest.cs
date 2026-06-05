// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyConstructor = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.MemberDocumentationAnalyzer,
    StyleSharp.Analyzers.ConstructorSummaryCodeFixProvider>;
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
        const string source = "/// <summary>A container.</summary>\n"
            + "public class C\n{\n"
            + "    /// <summary>Gets or sets the count.</summary>\n"
            + "    public int Count { get; set; }\n}";

        await VerifyProperty.VerifyAnalyzerAsync(source);
    }

    /// <summary>Verifies a property summary is reported and prefixed with the accessor phrase (SST1623).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task PropertyAccessorsAsync()
    {
        const string source = "/// <summary>A container.</summary>\n"
            + "public class C\n{\n"
            + "    /// {|SST1623:<summary>The count.</summary>|}\n"
            + "    public int Count { get; set; }\n}";
        const string fixedSource = "/// <summary>A container.</summary>\n"
            + "public class C\n{\n"
            + "    /// <summary>Gets or sets the count.</summary>\n"
            + "    public int Count { get; set; }\n}";

        await VerifyProperty.VerifyCodeFixAsync(source, fixedSource);
    }

    /// <summary>Verifies a constructor summary with the standard text is accepted.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ValidConstructorAsync()
    {
        const string source = "/// <summary>A container.</summary>\n"
            + "public class C\n{\n"
            + "    /// <summary>Initializes a new instance of the <see cref=\"C\"/> class.</summary>\n"
            + "    public C() { }\n}";

        await VerifyConstructor.VerifyAnalyzerAsync(source);
    }

    /// <summary>Verifies a constructor summary is reported and rewritten to the standard text (SST1642).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ConstructorStandardTextAsync()
    {
        const string source = "/// <summary>A container.</summary>\n"
            + "public class C\n{\n"
            + "    /// {|SST1642:<summary>Creates the thing.</summary>|}\n"
            + "    public C() { }\n}";
        const string fixedSource = "/// <summary>A container.</summary>\n"
            + "public class C\n{\n"
            + "    /// <summary>Initializes a new instance of the <see cref=\"C\"/> class.</summary>\n"
            + "    public C() { }\n}";

        await VerifyConstructor.VerifyCodeFixAsync(source, fixedSource);
    }
}
