// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using RoslynCommon.Analyzers.Tests;

namespace SecuritySharp.Analyzers.Tests;

/// <summary>Tests the in-order attribute scan against a stop marker and a recorded marker.</summary>
public class ComponentAttributeScanTests
{
    /// <summary>The marker attribute classes and the declarations scanned.</summary>
    private const string Source =
        """
        class RouteAttribute : System.Attribute { }
        class AuthorizeAttribute : System.Attribute { }
        class TeamAuthorizeAttribute : AuthorizeAttribute { }
        class OtherAttribute : System.Attribute { }
        [Other][Authorize][Route] class A { }
        [Authorize, TeamAuthorize] class B { }
        [TeamAuthorize][Other] class D { }
        [Other] class E { }
        [Route][Authorize] class F { }
        class G { }
        """;

    /// <summary>Verifies the scan stops at the first stop-marker match and records the first recorded-marker match before it.</summary>
    /// <param name="declaration">The scanned class name.</param>
    /// <param name="expectedStop">The name of the attribute that stopped the scan, or an empty string when none did.</param>
    /// <param name="expectedRecorded">The name of the recorded attribute, or an empty string when none was recorded.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("A", "Route", "Authorize")]
    [Arguments("B", "", "Authorize")]
    [Arguments("D", "", "TeamAuthorize")]
    [Arguments("E", "", "")]
    [Arguments("F", "Route", "")]
    [Arguments("G", "", "")]
    public async Task ScanStopsAtTheStopMarkerAndRecordsTheFirstMatchBeforeItAsync(string declaration, string expectedStop, string expectedRecorded)
    {
        var (model, compilation, lists) = await BindAsync(declaration);

        var stop = ComponentAttributeScan.FindFirst(
            model,
            lists,
            compilation.GetTypeByMetadataName("RouteAttribute")!,
            compilation.GetTypeByMetadataName("AuthorizeAttribute")!,
            CancellationToken.None,
            out var recorded);

        await Assert.That(stop?.Name.ToString() ?? string.Empty).IsEqualTo(expectedStop);
        await Assert.That(recorded?.Name.ToString() ?? string.Empty).IsEqualTo(expectedRecorded);
    }

    /// <summary>Verifies a subclass of the stop marker stops the scan and nothing is recorded without a recorded marker.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task SubclassStopsTheScanWithoutARecordedMarkerAsync()
    {
        var (model, compilation, lists) = await BindAsync("D");

        var stop = ComponentAttributeScan.FindFirst(model, lists, compilation.GetTypeByMetadataName("AuthorizeAttribute")!, null, CancellationToken.None, out var recorded);

        await Assert.That(stop?.Name.ToString()).IsEqualTo("TeamAuthorize");
        await Assert.That(recorded).IsNull();
    }

    /// <summary>Compiles the source and returns the attribute lists of one class.</summary>
    /// <param name="declaration">The class name.</param>
    /// <returns>The semantic model, the compilation, and the class's attribute lists.</returns>
    private static async Task<ScannedDeclaration> BindAsync(string declaration)
    {
        var tree = CSharpSyntaxTree.ParseText(Source);
        var compilation = CSharpCompilation.Create(nameof(ComponentAttributeScanTests), [tree], [RuntimeMetadataReferences.CoreLibrary]);
        var type = (await tree.GetRootAsync()).DescendantNodes().OfType<ClassDeclarationSyntax>().Single(c => c.Identifier.ValueText == declaration);
        return new(compilation.GetSemanticModel(tree), compilation, type.AttributeLists);
    }
}
