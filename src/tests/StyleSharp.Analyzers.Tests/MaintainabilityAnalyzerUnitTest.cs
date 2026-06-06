// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyAccess = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.AccessModifierAnalyzer,
    StyleSharp.Analyzers.AccessModifierCodeFixProvider>;
using VerifyDebug = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    StyleSharp.Analyzers.DebugMessageAnalyzer>;
using VerifyField = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    StyleSharp.Analyzers.FieldVisibilityAnalyzer>;
using VerifyFile = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    StyleSharp.Analyzers.FileTypeNamespaceAnalyzer>;
using VerifyParens = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.RedundantParenthesesAnalyzer,
    StyleSharp.Analyzers.RedundantParenthesesCodeFixProvider>;
using VerifyPrecedence = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.PrecedenceAnalyzer,
    StyleSharp.Analyzers.PrecedenceCodeFixProvider>;
using VerifySuppress = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    StyleSharp.Analyzers.SuppressionJustificationAnalyzer>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for the maintainability rules (SST1400–SST1411).</summary>
public class MaintainabilityAnalyzerUnitTest
{
    /// <summary>Verifies a top-level type with no access modifier is reported (SST1400) and gets 'internal'.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task TopLevelTypeGetsInternalAsync()
        => await VerifyAccess.VerifyCodeFixAsync("class {|SST1400:C|} { }", "internal class C { }");

    /// <summary>Verifies a member with no access modifier is reported (SST1400) and gets 'private'.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task MemberGetsPrivateAsync()
    {
        const string Source = "internal class C\n{\n    void {|SST1400:M|}() { }\n}";
        const string FixedSource = "internal class C\n{\n    private void M() { }\n}";
        await VerifyAccess.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies an interface member is not required to declare accessibility.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task InterfaceMemberAllowedAsync()
        => await VerifyAccess.VerifyAnalyzerAsync("internal interface I\n{\n    void M();\n}");

    /// <summary>Verifies an exposed field is reported (SST1401) while constants and private fields are not.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ExposedFieldReportedAsync()
        => await VerifyField.VerifyAnalyzerAsync(
            "internal class C\n{\n"
            + "    public int {|SST1401:Exposed|};\n"
            + "    public const int Allowed = 1;\n"
            + "    private int _hidden;\n}");

    /// <summary>Verifies a second top-level type is reported (SST1402).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SecondTypeReportedAsync()
        => await VerifyFile.VerifyAnalyzerAsync("internal class A { }\ninternal class {|SST1402:B|} { }");

    /// <summary>Verifies a partial type split across declarations is counted once.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task PartialTypeCountedOnceAsync()
        => await VerifyFile.VerifyAnalyzerAsync("internal partial class A { }\ninternal partial class A { }");

    /// <summary>Verifies a second namespace is reported (SST1403).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SecondNamespaceReportedAsync()
        => await VerifyFile.VerifyAnalyzerAsync("namespace A { }\nnamespace {|SST1403:B|} { }");

    /// <summary>Verifies a suppression without justification is reported (SST1404).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SuppressionWithoutJustificationReportedAsync()
        => await VerifySuppress.VerifyAnalyzerAsync(
            "using System.Diagnostics.CodeAnalysis;\n"
            + "internal class C\n{\n"
            + "    [{|SST1404:SuppressMessage(\"Cat\", \"Rule\")|}]\n"
            + "    public void M() { }\n}");

    /// <summary>Verifies a justified suppression is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task JustifiedSuppressionAllowedAsync()
        => await VerifySuppress.VerifyAnalyzerAsync(
            "using System.Diagnostics.CodeAnalysis;\n"
            + "internal class C\n{\n"
            + "    [SuppressMessage(\"Cat\", \"Rule\", Justification = \"Tested.\")]\n"
            + "    public void M() { }\n}");

    /// <summary>Verifies a Debug.Assert without a message is reported (SST1405).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task AssertWithoutMessageReportedAsync()
        => await VerifyDebug.VerifyAnalyzerAsync(
            "using System.Diagnostics;\n"
            + "internal class C\n{\n"
            + "    public void M() => Debug.{|SST1405:Assert|}(true);\n}");

    /// <summary>Verifies a Debug.Fail with an empty message is reported (SST1406).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task FailWithoutMessageReportedAsync()
        => await VerifyDebug.VerifyAnalyzerAsync(
            "using System.Diagnostics;\n"
            + "internal class C\n{\n"
            + "    public void M() => Debug.{|SST1406:Fail|}(\"\");\n}");

    /// <summary>Verifies mixed arithmetic precedence is reported (SST1407) and parenthesized.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ArithmeticPrecedenceParenthesizedAsync()
    {
        const string Source = "internal class C\n{\n    public int M(int a, int b, int c) => a + {|SST1407:b * c|};\n}";
        const string FixedSource = "internal class C\n{\n    public int M(int a, int b, int c) => a + (b * c);\n}";
        await VerifyPrecedence.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies mixed conditional precedence is reported (SST1408) and parenthesized.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ConditionalPrecedenceParenthesizedAsync()
    {
        const string Source = "internal class C\n{\n    public bool M(bool a, bool b, bool c) => a || {|SST1408:b && c|};\n}";
        const string FixedSource = "internal class C\n{\n    public bool M(bool a, bool b, bool c) => a || (b && c);\n}";
        await VerifyPrecedence.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies an empty anonymous-method parameter list is reported (SST1410) and removed.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task EmptyDelegateParenthesesRemovedAsync()
    {
        const string Source = "using System;\ninternal class C\n{\n    public Action A() => delegate{|SST1410:()|} { };\n}";
        const string FixedSource = "using System;\ninternal class C\n{\n    public Action A() => delegate { };\n}";
        await VerifyParens.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies an empty attribute argument list is reported (SST1411) and removed.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task EmptyAttributeParenthesesRemovedAsync()
    {
        const string Source = "internal class C\n{\n    [System.Obsolete{|SST1411:()|}]\n    public void M() { }\n}";
        const string FixedSource = "internal class C\n{\n    [System.Obsolete]\n    public void M() { }\n}";
        await VerifyParens.VerifyCodeFixAsync(Source, FixedSource);
    }
}
