// Copyright (c) 2023 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace Blazor.Common.Analyzers.Tests;

#pragma warning disable SA1402
#pragma warning disable SA1649
#pragma warning disable SA1507
#pragma warning disable SA1518


using VerifyRCGS0001 = CSharpCodeFixVerifier<
    RCGS0001ConstructorDeclarationParameterMustBeOnUniqueLinesAnalyzer,
    RCGS0001ConstructorDeclarationParameterMustBeOnUniqueLinesCodeFixProvider>;

using VerifyRCGS0002 = CSharpCodeFixVerifier<
    RCGS0002MethodDeclarationParameterMustBeOnUniqueLinesAnalyzer,
    RCGS0002MethodDeclarationParameterMustBeOnUniqueLinesCodeFixProvider>;

using VerifyRCGS0003 = CSharpCodeFixVerifier<
    RCGS0003DelegateDeclarationParameterMustBeOnUniqueLinesAnalyzer,
    RCGS0003DelegateDeclarationParameterMustBeOnUniqueLinesCodeFixProvider>;

using VerifyRCGS0004 = CSharpCodeFixVerifier<
    RCGS0004IndexerDeclarationParameterMustBeOnUniqueLinesAnalyzer,
    RCGS0004IndexerDeclarationParameterMustBeOnUniqueLinesCodeFixProvider>;

using VerifyRCGS0005 = CSharpCodeFixVerifier<
    RCGS0005InvocationExpressionArgumentMustBeOnUniqueLinesAnalyzer,
    RCGS0005InvocationExpressionArgumentMustBeOnUniqueLinesCodeFixProvider>;

using VerifyRCGS0006 = CSharpCodeFixVerifier<
    RCGS0006ObjectCreationExpressionArgumentMustBeOnUniqueLinesAnalyzer,
    RCGS0006ObjectCreationExpressionArgumentMustBeOnUniqueLinesCodeFixProvider>;

using VerifyRCGS0007 = CSharpCodeFixVerifier<
    RCGS0007ElementAccessExpressionArgumentMustBeOnUniqueLinesAnalyzer,
    RCGS0007ElementAccessExpressionArgumentMustBeOnUniqueLinesCodeFixProvider>;

using VerifyRCGS0008 = CSharpCodeFixVerifier<
    RCGS0008AttributeArgumentMustBeOnUniqueLinesAnalyzer,
    RCGS0008AttributeArgumentMustBeOnUniqueLinesCodeFixProvider>;

using VerifyRCGS0009 = CSharpCodeFixVerifier<
    RCGS0009AnonymousMethodExpressionParameterMustBeOnUniqueLinesAnalyzer,
    RCGS0009AnonymousMethodExpressionParameterMustBeOnUniqueLinesCodeFixProvider>;

using VerifyRCGS0010 = CSharpCodeFixVerifier<
    RCGS0010ParenthesizedLambdaExpressionParameterMustBeOnUniqueLinesAnalyzer,
    RCGS0010ParenthesizedLambdaExpressionParameterMustBeOnUniqueLinesCodeFixProvider>;

[TestClass]
public partial class RCGS0001ConstructorDeclarationAnalyzersUnitTest
{
    [TestMethod]
    public async Task Empty()
    {
        var test = string.Empty;

        await VerifyRCGS0001.VerifyAnalyzerAsync(test);
    }

    [TestMethod]
    [DataRow(1)]
    [DataRow(2)]
    [DataRow(3)]
    [DataRow(4)]
    [DataRow(5)]
    [DataRow(6)]
    [DataRow(7)]
    [DataRow(8)]
    public async Task ValidFlatEntry(int number)
    {
        var classGenerator = new ClassGeneratorBuilder();
        classGenerator.Init();
        classGenerator.ConstructorDeclaration(number);
        var test = classGenerator.Generate();
        await VerifyRCGS0001.VerifyAnalyzerAsync(test);
    }

    [TestMethod]
    [DataRow(2)]
    [DataRow(3)]
    [DataRow(4)]
    [DataRow(5)]
    [DataRow(6)]
    [DataRow(7)]
    [DataRow(8)]
    public async Task InvalidJaggeredEntry(int number)
    {
        var (test, startLine, startColumn, endLine, endColumn) = GenerateJaggered(number);
        await VerifyRCGS0001.VerifyAnalyzerAsync(test, VerifyRCGS0001.Diagnostic("RCGS0001").WithSpan(startLine, startColumn, endLine, endColumn));
    }

    [TestMethod]
    [DataRow(2)]
    [DataRow(3)]
    [DataRow(4)]
    [DataRow(5)]
    [DataRow(6)]
    [DataRow(7)]
    [DataRow(8)]
    public async Task ValidCodeFix(int number)
    {
        var (test, startLine, startColumn, endLine, endColumn) = GenerateJaggered(number);
        var fixtest = GenerateStaggered(number);
        var expected = VerifyRCGS0001.Diagnostic("RCGS0001").WithSpan(startLine, startColumn, endLine, endColumn);
        await VerifyRCGS0001.VerifyCodeFixAsync(test, expected, fixtest);
    }

    private static (string Text, int StartLine, int StartColumn, int EndLine, int EndColumn) GenerateJaggered(int number)
    {
        var classGenerator = new ClassGeneratorBuilder();
        classGenerator.Init();
        var (startLine, startColumn, endLine, endColumn) = classGenerator.ConstructorDeclarationJaggered(number);
        return (classGenerator.Generate(), startLine, startColumn, endLine, endColumn);
    }

    private static string GenerateStaggered(int number)
    {
        var classGenerator = new ClassGeneratorBuilder();
        classGenerator.Init();
        classGenerator.ConstructorDeclarationStaggered(number);
        return classGenerator.Generate();
    }
}

[TestClass]
public partial class RCGS0002MethodDeclarationAnalyzersUnitTest
{
    [TestMethod]
    public async Task Empty()
    {
        var test = string.Empty;

        await VerifyRCGS0002.VerifyAnalyzerAsync(test);
    }

    [TestMethod]
    [DataRow(1)]
    [DataRow(2)]
    [DataRow(3)]
    [DataRow(4)]
    [DataRow(5)]
    [DataRow(6)]
    [DataRow(7)]
    [DataRow(8)]
    public async Task ValidFlatEntry(int number)
    {
        var classGenerator = new ClassGeneratorBuilder();
        classGenerator.Init();
        classGenerator.MethodDeclaration(number);
        var test = classGenerator.Generate();
        await VerifyRCGS0002.VerifyAnalyzerAsync(test);
    }

    [TestMethod]
    [DataRow(2)]
    [DataRow(3)]
    [DataRow(4)]
    [DataRow(5)]
    [DataRow(6)]
    [DataRow(7)]
    [DataRow(8)]
    public async Task InvalidJaggeredEntry(int number)
    {
        var (test, startLine, startColumn, endLine, endColumn) = GenerateJaggered(number);
        await VerifyRCGS0002.VerifyAnalyzerAsync(test, VerifyRCGS0002.Diagnostic("RCGS0002").WithSpan(startLine, startColumn, endLine, endColumn));
    }

    [TestMethod]
    [DataRow(2)]
    [DataRow(3)]
    [DataRow(4)]
    [DataRow(5)]
    [DataRow(6)]
    [DataRow(7)]
    [DataRow(8)]
    public async Task ValidCodeFix(int number)
    {
        var (test, startLine, startColumn, endLine, endColumn) = GenerateJaggered(number);
        var fixtest = GenerateStaggered(number);
        var expected = VerifyRCGS0002.Diagnostic("RCGS0002").WithSpan(startLine, startColumn, endLine, endColumn);
        await VerifyRCGS0002.VerifyCodeFixAsync(test, expected, fixtest);
    }

    private static (string Text, int StartLine, int StartColumn, int EndLine, int EndColumn) GenerateJaggered(int number)
    {
        var classGenerator = new ClassGeneratorBuilder();
        classGenerator.Init();
        var (startLine, startColumn, endLine, endColumn) = classGenerator.MethodDeclarationJaggered(number);
        return (classGenerator.Generate(), startLine, startColumn, endLine, endColumn);
    }

    private static string GenerateStaggered(int number)
    {
        var classGenerator = new ClassGeneratorBuilder();
        classGenerator.Init();
        classGenerator.MethodDeclarationStaggered(number);
        return classGenerator.Generate();
    }
}

[TestClass]
public partial class RCGS0003DelegateDeclarationAnalyzersUnitTest
{
    [TestMethod]
    public async Task Empty()
    {
        var test = string.Empty;

        await VerifyRCGS0003.VerifyAnalyzerAsync(test);
    }

    [TestMethod]
    [DataRow(1)]
    [DataRow(2)]
    [DataRow(3)]
    [DataRow(4)]
    [DataRow(5)]
    [DataRow(6)]
    [DataRow(7)]
    [DataRow(8)]
    public async Task ValidFlatEntry(int number)
    {
        var classGenerator = new ClassGeneratorBuilder();
        classGenerator.Init();
        classGenerator.DelegateDeclaration(number);
        var test = classGenerator.Generate();
        await VerifyRCGS0003.VerifyAnalyzerAsync(test);
    }

    [TestMethod]
    [DataRow(2)]
    [DataRow(3)]
    [DataRow(4)]
    [DataRow(5)]
    [DataRow(6)]
    [DataRow(7)]
    [DataRow(8)]
    public async Task InvalidJaggeredEntry(int number)
    {
        var (test, startLine, startColumn, endLine, endColumn) = GenerateJaggered(number);
        await VerifyRCGS0003.VerifyAnalyzerAsync(test, VerifyRCGS0003.Diagnostic("RCGS0003").WithSpan(startLine, startColumn, endLine, endColumn));
    }

    [TestMethod]
    [DataRow(2)]
    [DataRow(3)]
    [DataRow(4)]
    [DataRow(5)]
    [DataRow(6)]
    [DataRow(7)]
    [DataRow(8)]
    public async Task ValidCodeFix(int number)
    {
        var (test, startLine, startColumn, endLine, endColumn) = GenerateJaggered(number);
        var fixtest = GenerateStaggered(number);
        var expected = VerifyRCGS0003.Diagnostic("RCGS0003").WithSpan(startLine, startColumn, endLine, endColumn);
        await VerifyRCGS0003.VerifyCodeFixAsync(test, expected, fixtest);
    }

    private static (string Text, int StartLine, int StartColumn, int EndLine, int EndColumn) GenerateJaggered(int number)
    {
        var classGenerator = new ClassGeneratorBuilder();
        classGenerator.Init();
        var (startLine, startColumn, endLine, endColumn) = classGenerator.DelegateDeclarationJaggered(number);
        return (classGenerator.Generate(), startLine, startColumn, endLine, endColumn);
    }

    private static string GenerateStaggered(int number)
    {
        var classGenerator = new ClassGeneratorBuilder();
        classGenerator.Init();
        classGenerator.DelegateDeclarationStaggered(number);
        return classGenerator.Generate();
    }
}

[TestClass]
public partial class RCGS0004IndexerDeclarationAnalyzersUnitTest
{
    [TestMethod]
    public async Task Empty()
    {
        var test = string.Empty;

        await VerifyRCGS0004.VerifyAnalyzerAsync(test);
    }

    [TestMethod]
    [DataRow(1)]
    [DataRow(2)]
    [DataRow(3)]
    [DataRow(4)]
    [DataRow(5)]
    [DataRow(6)]
    [DataRow(7)]
    [DataRow(8)]
    public async Task ValidFlatEntry(int number)
    {
        var classGenerator = new ClassGeneratorBuilder();
        classGenerator.Init();
        classGenerator.IndexerDeclaration(number);
        var test = classGenerator.Generate();
        await VerifyRCGS0004.VerifyAnalyzerAsync(test);
    }

    [TestMethod]
    [DataRow(2)]
    [DataRow(3)]
    [DataRow(4)]
    [DataRow(5)]
    [DataRow(6)]
    [DataRow(7)]
    [DataRow(8)]
    public async Task InvalidJaggeredEntry(int number)
    {
        var (test, startLine, startColumn, endLine, endColumn) = GenerateJaggered(number);
        await VerifyRCGS0004.VerifyAnalyzerAsync(test, VerifyRCGS0004.Diagnostic("RCGS0004").WithSpan(startLine, startColumn, endLine, endColumn));
    }

    [TestMethod]
    [DataRow(2)]
    [DataRow(3)]
    [DataRow(4)]
    [DataRow(5)]
    [DataRow(6)]
    [DataRow(7)]
    [DataRow(8)]
    public async Task ValidCodeFix(int number)
    {
        var (test, startLine, startColumn, endLine, endColumn) = GenerateJaggered(number);
        var fixtest = GenerateStaggered(number);
        var expected = VerifyRCGS0004.Diagnostic("RCGS0004").WithSpan(startLine, startColumn, endLine, endColumn);
        await VerifyRCGS0004.VerifyCodeFixAsync(test, expected, fixtest);
    }

    private static (string Text, int StartLine, int StartColumn, int EndLine, int EndColumn) GenerateJaggered(int number)
    {
        var classGenerator = new ClassGeneratorBuilder();
        classGenerator.Init();
        var (startLine, startColumn, endLine, endColumn) = classGenerator.IndexerDeclarationJaggered(number);
        return (classGenerator.Generate(), startLine, startColumn, endLine, endColumn);
    }

    private static string GenerateStaggered(int number)
    {
        var classGenerator = new ClassGeneratorBuilder();
        classGenerator.Init();
        classGenerator.IndexerDeclarationStaggered(number);
        return classGenerator.Generate();
    }
}

[TestClass]
public partial class RCGS0005InvocationExpressionAnalyzersUnitTest
{
    [TestMethod]
    public async Task Empty()
    {
        var test = string.Empty;

        await VerifyRCGS0005.VerifyAnalyzerAsync(test);
    }

    [TestMethod]
    [DataRow(1)]
    [DataRow(2)]
    [DataRow(3)]
    [DataRow(4)]
    [DataRow(5)]
    [DataRow(6)]
    [DataRow(7)]
    [DataRow(8)]
    public async Task ValidFlatEntry(int number)
    {
        var classGenerator = new ClassGeneratorBuilder();
        classGenerator.Init();
        classGenerator.InvocationExpression(number);
        var test = classGenerator.Generate();
        await VerifyRCGS0005.VerifyAnalyzerAsync(test);
    }

    [TestMethod]
    [DataRow(2)]
    [DataRow(3)]
    [DataRow(4)]
    [DataRow(5)]
    [DataRow(6)]
    [DataRow(7)]
    [DataRow(8)]
    public async Task InvalidJaggeredEntry(int number)
    {
        var (test, startLine, startColumn, endLine, endColumn) = GenerateJaggered(number);
        await VerifyRCGS0005.VerifyAnalyzerAsync(test, VerifyRCGS0005.Diagnostic("RCGS0005").WithSpan(startLine, startColumn, endLine, endColumn));
    }

    [TestMethod]
    [DataRow(2)]
    [DataRow(3)]
    [DataRow(4)]
    [DataRow(5)]
    [DataRow(6)]
    [DataRow(7)]
    [DataRow(8)]
    public async Task ValidCodeFix(int number)
    {
        var (test, startLine, startColumn, endLine, endColumn) = GenerateJaggered(number);
        var fixtest = GenerateStaggered(number);
        var expected = VerifyRCGS0005.Diagnostic("RCGS0005").WithSpan(startLine, startColumn, endLine, endColumn);
        await VerifyRCGS0005.VerifyCodeFixAsync(test, expected, fixtest);
    }

    private static (string Text, int StartLine, int StartColumn, int EndLine, int EndColumn) GenerateJaggered(int number)
    {
        var classGenerator = new ClassGeneratorBuilder();
        classGenerator.Init();
        var (startLine, startColumn, endLine, endColumn) = classGenerator.InvocationExpressionJaggered(number);
        return (classGenerator.Generate(), startLine, startColumn, endLine, endColumn);
    }

    private static string GenerateStaggered(int number)
    {
        var classGenerator = new ClassGeneratorBuilder();
        classGenerator.Init();
        classGenerator.InvocationExpressionStaggered(number);
        return classGenerator.Generate();
    }
}

[TestClass]
public partial class RCGS0006ObjectCreationExpressionAnalyzersUnitTest
{
    [TestMethod]
    public async Task Empty()
    {
        var test = string.Empty;

        await VerifyRCGS0006.VerifyAnalyzerAsync(test);
    }

    [TestMethod]
    [DataRow(1)]
    [DataRow(2)]
    [DataRow(3)]
    [DataRow(4)]
    [DataRow(5)]
    [DataRow(6)]
    [DataRow(7)]
    [DataRow(8)]
    public async Task ValidFlatEntry(int number)
    {
        var classGenerator = new ClassGeneratorBuilder();
        classGenerator.Init();
        classGenerator.ObjectCreationExpression(number);
        var test = classGenerator.Generate();
        await VerifyRCGS0006.VerifyAnalyzerAsync(test);
    }

    [TestMethod]
    [DataRow(2)]
    [DataRow(3)]
    [DataRow(4)]
    [DataRow(5)]
    [DataRow(6)]
    [DataRow(7)]
    [DataRow(8)]
    public async Task InvalidJaggeredEntry(int number)
    {
        var (test, startLine, startColumn, endLine, endColumn) = GenerateJaggered(number);
        await VerifyRCGS0006.VerifyAnalyzerAsync(test, VerifyRCGS0006.Diagnostic("RCGS0006").WithSpan(startLine, startColumn, endLine, endColumn));
    }

    [TestMethod]
    [DataRow(2)]
    [DataRow(3)]
    [DataRow(4)]
    [DataRow(5)]
    [DataRow(6)]
    [DataRow(7)]
    [DataRow(8)]
    public async Task ValidCodeFix(int number)
    {
        var (test, startLine, startColumn, endLine, endColumn) = GenerateJaggered(number);
        var fixtest = GenerateStaggered(number);
        var expected = VerifyRCGS0006.Diagnostic("RCGS0006").WithSpan(startLine, startColumn, endLine, endColumn);
        await VerifyRCGS0006.VerifyCodeFixAsync(test, expected, fixtest);
    }

    private static (string Text, int StartLine, int StartColumn, int EndLine, int EndColumn) GenerateJaggered(int number)
    {
        var classGenerator = new ClassGeneratorBuilder();
        classGenerator.Init();
        var (startLine, startColumn, endLine, endColumn) = classGenerator.ObjectCreationExpressionJaggered(number);
        return (classGenerator.Generate(), startLine, startColumn, endLine, endColumn);
    }

    private static string GenerateStaggered(int number)
    {
        var classGenerator = new ClassGeneratorBuilder();
        classGenerator.Init();
        classGenerator.ObjectCreationExpressionStaggered(number);
        return classGenerator.Generate();
    }
}

[TestClass]
public partial class RCGS0007ElementAccessExpressionAnalyzersUnitTest
{
    [TestMethod]
    public async Task Empty()
    {
        var test = string.Empty;

        await VerifyRCGS0007.VerifyAnalyzerAsync(test);
    }

    [TestMethod]
    [DataRow(1)]
    [DataRow(2)]
    [DataRow(3)]
    [DataRow(4)]
    [DataRow(5)]
    [DataRow(6)]
    [DataRow(7)]
    [DataRow(8)]
    public async Task ValidFlatEntry(int number)
    {
        var classGenerator = new ClassGeneratorBuilder();
        classGenerator.Init();
        classGenerator.ElementAccessExpression(number);
        var test = classGenerator.Generate();
        await VerifyRCGS0007.VerifyAnalyzerAsync(test);
    }

    [TestMethod]
    [DataRow(2)]
    [DataRow(3)]
    [DataRow(4)]
    [DataRow(5)]
    [DataRow(6)]
    [DataRow(7)]
    [DataRow(8)]
    public async Task InvalidJaggeredEntry(int number)
    {
        var (test, startLine, startColumn, endLine, endColumn) = GenerateJaggered(number);
        await VerifyRCGS0007.VerifyAnalyzerAsync(test, VerifyRCGS0007.Diagnostic("RCGS0007").WithSpan(startLine, startColumn, endLine, endColumn));
    }

    [TestMethod]
    [DataRow(2)]
    [DataRow(3)]
    [DataRow(4)]
    [DataRow(5)]
    [DataRow(6)]
    [DataRow(7)]
    [DataRow(8)]
    public async Task ValidCodeFix(int number)
    {
        var (test, startLine, startColumn, endLine, endColumn) = GenerateJaggered(number);
        var fixtest = GenerateStaggered(number);
        var expected = VerifyRCGS0007.Diagnostic("RCGS0007").WithSpan(startLine, startColumn, endLine, endColumn);
        await VerifyRCGS0007.VerifyCodeFixAsync(test, expected, fixtest);
    }

    private static (string Text, int StartLine, int StartColumn, int EndLine, int EndColumn) GenerateJaggered(int number)
    {
        var classGenerator = new ClassGeneratorBuilder();
        classGenerator.Init();
        var (startLine, startColumn, endLine, endColumn) = classGenerator.ElementAccessExpressionJaggered(number);
        return (classGenerator.Generate(), startLine, startColumn, endLine, endColumn);
    }

    private static string GenerateStaggered(int number)
    {
        var classGenerator = new ClassGeneratorBuilder();
        classGenerator.Init();
        classGenerator.ElementAccessExpressionStaggered(number);
        return classGenerator.Generate();
    }
}

[TestClass]
public partial class RCGS0008AttributeAnalyzersUnitTest
{
    [TestMethod]
    public async Task Empty()
    {
        var test = string.Empty;

        await VerifyRCGS0008.VerifyAnalyzerAsync(test);
    }

    [TestMethod]
    [DataRow(1)]
    [DataRow(2)]
    [DataRow(3)]
    [DataRow(4)]
    [DataRow(5)]
    [DataRow(6)]
    [DataRow(7)]
    [DataRow(8)]
    public async Task ValidFlatEntry(int number)
    {
        var classGenerator = new ClassGeneratorBuilder();
        classGenerator.Init();
        classGenerator.Attribute(number);
        var test = classGenerator.Generate();
        await VerifyRCGS0008.VerifyAnalyzerAsync(test);
    }

    [TestMethod]
    [DataRow(2)]
    [DataRow(3)]
    [DataRow(4)]
    [DataRow(5)]
    [DataRow(6)]
    [DataRow(7)]
    [DataRow(8)]
    public async Task InvalidJaggeredEntry(int number)
    {
        var (test, startLine, startColumn, endLine, endColumn) = GenerateJaggered(number);
        await VerifyRCGS0008.VerifyAnalyzerAsync(test, VerifyRCGS0008.Diagnostic("RCGS0008").WithSpan(startLine, startColumn, endLine, endColumn));
    }

    [TestMethod]
    [DataRow(2)]
    [DataRow(3)]
    [DataRow(4)]
    [DataRow(5)]
    [DataRow(6)]
    [DataRow(7)]
    [DataRow(8)]
    public async Task ValidCodeFix(int number)
    {
        var (test, startLine, startColumn, endLine, endColumn) = GenerateJaggered(number);
        var fixtest = GenerateStaggered(number);
        var expected = VerifyRCGS0008.Diagnostic("RCGS0008").WithSpan(startLine, startColumn, endLine, endColumn);
        await VerifyRCGS0008.VerifyCodeFixAsync(test, expected, fixtest);
    }

    private static (string Text, int StartLine, int StartColumn, int EndLine, int EndColumn) GenerateJaggered(int number)
    {
        var classGenerator = new ClassGeneratorBuilder();
        classGenerator.Init();
        var (startLine, startColumn, endLine, endColumn) = classGenerator.AttributeJaggered(number);
        return (classGenerator.Generate(), startLine, startColumn, endLine, endColumn);
    }

    private static string GenerateStaggered(int number)
    {
        var classGenerator = new ClassGeneratorBuilder();
        classGenerator.Init();
        classGenerator.AttributeStaggered(number);
        return classGenerator.Generate();
    }
}

[TestClass]
public partial class RCGS0009AnonymousMethodExpressionAnalyzersUnitTest
{
    [TestMethod]
    public async Task Empty()
    {
        var test = string.Empty;

        await VerifyRCGS0009.VerifyAnalyzerAsync(test);
    }

    [TestMethod]
    [DataRow(1)]
    [DataRow(2)]
    [DataRow(3)]
    [DataRow(4)]
    [DataRow(5)]
    [DataRow(6)]
    [DataRow(7)]
    [DataRow(8)]
    public async Task ValidFlatEntry(int number)
    {
        var classGenerator = new ClassGeneratorBuilder();
        classGenerator.Init();
        classGenerator.AnonymousMethodExpression(number);
        var test = classGenerator.Generate();
        await VerifyRCGS0009.VerifyAnalyzerAsync(test);
    }

    [TestMethod]
    [DataRow(2)]
    [DataRow(3)]
    [DataRow(4)]
    [DataRow(5)]
    [DataRow(6)]
    [DataRow(7)]
    [DataRow(8)]
    public async Task InvalidJaggeredEntry(int number)
    {
        var (test, startLine, startColumn, endLine, endColumn) = GenerateJaggered(number);
        await VerifyRCGS0009.VerifyAnalyzerAsync(test, VerifyRCGS0009.Diagnostic("RCGS0009").WithSpan(startLine, startColumn, endLine, endColumn));
    }

    [TestMethod]
    [DataRow(2)]
    [DataRow(3)]
    [DataRow(4)]
    [DataRow(5)]
    [DataRow(6)]
    [DataRow(7)]
    [DataRow(8)]
    public async Task ValidCodeFix(int number)
    {
        var (test, startLine, startColumn, endLine, endColumn) = GenerateJaggered(number);
        var fixtest = GenerateStaggered(number);
        var expected = VerifyRCGS0009.Diagnostic("RCGS0009").WithSpan(startLine, startColumn, endLine, endColumn);
        await VerifyRCGS0009.VerifyCodeFixAsync(test, expected, fixtest);
    }

    private static (string Text, int StartLine, int StartColumn, int EndLine, int EndColumn) GenerateJaggered(int number)
    {
        var classGenerator = new ClassGeneratorBuilder();
        classGenerator.Init();
        var (startLine, startColumn, endLine, endColumn) = classGenerator.AnonymousMethodExpressionJaggered(number);
        return (classGenerator.Generate(), startLine, startColumn, endLine, endColumn);
    }

    private static string GenerateStaggered(int number)
    {
        var classGenerator = new ClassGeneratorBuilder();
        classGenerator.Init();
        classGenerator.AnonymousMethodExpressionStaggered(number);
        return classGenerator.Generate();
    }
}

[TestClass]
public partial class RCGS0010ParenthesizedLambdaExpressionAnalyzersUnitTest
{
    [TestMethod]
    public async Task Empty()
    {
        var test = string.Empty;

        await VerifyRCGS0010.VerifyAnalyzerAsync(test);
    }

    [TestMethod]
    [DataRow(1)]
    [DataRow(2)]
    [DataRow(3)]
    [DataRow(4)]
    [DataRow(5)]
    [DataRow(6)]
    [DataRow(7)]
    [DataRow(8)]
    public async Task ValidFlatEntry(int number)
    {
        var classGenerator = new ClassGeneratorBuilder();
        classGenerator.Init();
        classGenerator.ParenthesizedLambdaExpression(number);
        var test = classGenerator.Generate();
        await VerifyRCGS0010.VerifyAnalyzerAsync(test);
    }

    [TestMethod]
    [DataRow(2)]
    [DataRow(3)]
    [DataRow(4)]
    [DataRow(5)]
    [DataRow(6)]
    [DataRow(7)]
    [DataRow(8)]
    public async Task InvalidJaggeredEntry(int number)
    {
        var (test, startLine, startColumn, endLine, endColumn) = GenerateJaggered(number);
        await VerifyRCGS0010.VerifyAnalyzerAsync(test, VerifyRCGS0010.Diagnostic("RCGS0010").WithSpan(startLine, startColumn, endLine, endColumn));
    }

    [TestMethod]
    [DataRow(2)]
    [DataRow(3)]
    [DataRow(4)]
    [DataRow(5)]
    [DataRow(6)]
    [DataRow(7)]
    [DataRow(8)]
    public async Task ValidCodeFix(int number)
    {
        var (test, startLine, startColumn, endLine, endColumn) = GenerateJaggered(number);
        var fixtest = GenerateStaggered(number);
        var expected = VerifyRCGS0010.Diagnostic("RCGS0010").WithSpan(startLine, startColumn, endLine, endColumn);
        await VerifyRCGS0010.VerifyCodeFixAsync(test, expected, fixtest);
    }

    private static (string Text, int StartLine, int StartColumn, int EndLine, int EndColumn) GenerateJaggered(int number)
    {
        var classGenerator = new ClassGeneratorBuilder();
        classGenerator.Init();
        var (startLine, startColumn, endLine, endColumn) = classGenerator.ParenthesizedLambdaExpressionJaggered(number);
        return (classGenerator.Generate(), startLine, startColumn, endLine, endColumn);
    }

    private static string GenerateStaggered(int number)
    {
        var classGenerator = new ClassGeneratorBuilder();
        classGenerator.Init();
        classGenerator.ParenthesizedLambdaExpressionStaggered(number);
        return classGenerator.Generate();
    }
}

