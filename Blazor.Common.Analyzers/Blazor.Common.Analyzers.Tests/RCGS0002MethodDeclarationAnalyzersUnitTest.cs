using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using VerifyRCGS0002 = Blazor.Common.Analyzers.Tests.CSharpCodeFixVerifier<
    Blazor.Common.Analyzers.RCGS0002MethodDeclarationParameterMustBeOnUniqueLinesAnalyzer,
    Blazor.Common.Analyzers.RCGS0002MethodDeclarationParameterMustBeOnUniqueLinesCodeFixProvider>;

namespace Blazor.Common.Analyzers.Tests;

public partial class RCGS0002MethodDeclarationAnalyzersUnitTest
{
    [TestMethod]
    public async Task ValidClass_NoDiagnostic()
    {
        var test = @"using System;

public static class Configuration
{
    public static void AddDefaultHeader(string test, string test2)
    {
    }
}

public class Test
{
    /// <summary>
    /// Add default header.
    /// </summary>
    /// <param name=""key"">Header field name.</param>
    /// <param name=""value"">Header field value.</param>
    [Obsolete(""AddDefaultHeader is deprecated, please use Configuration.AddDefaultHeader instead."")]
    public void AddDefaultHeader(string key, string value)
    {
        Configuration.AddDefaultHeader(key, value);
    }
}
";

        await VerifyRCGS0002.VerifyAnalyzerAsync(test);
    }
}
