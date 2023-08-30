namespace RoslynCommonAnalyzers.Test;


using VerifyRCGS0001 = CSharpCodeFixVerifier<
    RCGS0001ConstructorDeclarationParameterMustBeOnUniqueLinesAnalyzer,
    RCGS0001ParameterMustBeOnUniqueLinesCodeFixProvider>;


using VerifyRCGS0002 = CSharpCodeFixVerifier<
    RCGS0002MethodDeclarationParameterMustBeOnUniqueLinesAnalyzer,
    RCGS0002ParameterMustBeOnUniqueLinesCodeFixProvider>;


using VerifyRCGS0003 = CSharpCodeFixVerifier<
    RCGS0003DelegateDeclarationParameterMustBeOnUniqueLinesAnalyzer,
    RCGS0003ParameterMustBeOnUniqueLinesCodeFixProvider>;


using VerifyRCGS0004 = CSharpCodeFixVerifier<
    RCGS0004IndexerDeclarationParameterMustBeOnUniqueLinesAnalyzer,
    RCGS0004ParameterMustBeOnUniqueLinesCodeFixProvider>;


using VerifyRCGS0005 = CSharpCodeFixVerifier<
    RCGS0005InvocationExpressionArgumentMustBeOnUniqueLinesAnalyzer,
    RCGS0005ArgumentMustBeOnUniqueLinesCodeFixProvider>;


using VerifyRCGS0006 = CSharpCodeFixVerifier<
    RCGS0006ObjectCreationExpressionArgumentMustBeOnUniqueLinesAnalyzer,
    RCGS0006ArgumentMustBeOnUniqueLinesCodeFixProvider>;


using VerifyRCGS0007 = CSharpCodeFixVerifier<
    RCGS0007ElementAccessExpressionArgumentMustBeOnUniqueLinesAnalyzer,
    RCGS0007ArgumentMustBeOnUniqueLinesCodeFixProvider>;


using VerifyRCGS0008 = CSharpCodeFixVerifier<
    RCGS0008AttributeArgumentMustBeOnUniqueLinesAnalyzer,
    RCGS0008ArgumentMustBeOnUniqueLinesCodeFixProvider>;


using VerifyRCGS0009 = CSharpCodeFixVerifier<
    RCGS0009AnonymousMethodExpressionParameterMustBeOnUniqueLinesAnalyzer,
    RCGS0009ParameterMustBeOnUniqueLinesCodeFixProvider>;


using VerifyRCGS0010 = CSharpCodeFixVerifier<
    RCGS0010ParenthesizedLambdaExpressionParameterMustBeOnUniqueLinesAnalyzer,
    RCGS0010ParameterMustBeOnUniqueLinesCodeFixProvider>;


[TestClass]
public class RCGS0001ConstructorDeclarationAnalyzersUnitTest
{
    [TestMethod]
    public async Task Empty()
    {
        var test = @"";

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
}

[TestClass]
public class RCGS0002MethodDeclarationAnalyzersUnitTest
{
    [TestMethod]
    public async Task Empty()
    {
        var test = @"";

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
}

[TestClass]
public class RCGS0003DelegateDeclarationAnalyzersUnitTest
{
    [TestMethod]
    public async Task Empty()
    {
        var test = @"";

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
}

[TestClass]
public class RCGS0004IndexerDeclarationAnalyzersUnitTest
{
    [TestMethod]
    public async Task Empty()
    {
        var test = @"";

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
}

[TestClass]
public class RCGS0005InvocationExpressionAnalyzersUnitTest
{
    [TestMethod]
    public async Task Empty()
    {
        var test = @"";

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
}

[TestClass]
public class RCGS0006ObjectCreationExpressionAnalyzersUnitTest
{
    [TestMethod]
    public async Task Empty()
    {
        var test = @"";

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
}

[TestClass]
public class RCGS0007ElementAccessExpressionAnalyzersUnitTest
{
    [TestMethod]
    public async Task Empty()
    {
        var test = @"";

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
}

[TestClass]
public class RCGS0008AttributeAnalyzersUnitTest
{
    [TestMethod]
    public async Task Empty()
    {
        var test = @"";

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
}

[TestClass]
public class RCGS0009AnonymousMethodExpressionAnalyzersUnitTest
{
    [TestMethod]
    public async Task Empty()
    {
        var test = @"";

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
}

[TestClass]
public class RCGS0010ParenthesizedLambdaExpressionAnalyzersUnitTest
{
    [TestMethod]
    public async Task Empty()
    {
        var test = @"";

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
}

