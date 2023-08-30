using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RoslynCommonAnalyzers.Test;

internal class ClassGeneratorBuilder
{
    private StringBuilder _builder = new();

    public void Init()
    {
        _builder.AppendLine(@"using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;

namespace ConsoleApplication1
{
    public class MyTestClass
    {");
    }

    public ClassGeneratorBuilder MethodDeclaration(int parameterCount)
    {
        _builder.AppendLine($@"
        public void MyMethod({GenerateOneLineParameters(parameterCount)})
        {{
        }}");

        return this;
    }

    public ClassGeneratorBuilder MethodDeclarationJaggered(int parameterCount)
    {
        _builder.AppendLine($@"
        public void MyMethod({GenerateJaggeredLineParameters(parameterCount)})
        {{
        }}");

        return this;
    }

    public ClassGeneratorBuilder MethodDeclarationStaggered(int parameterCount)
    {
        _builder.AppendLine($@"
        public void MyMethod({GenerateStaggeredLineParameters(parameterCount)})
        {{
        }}");

        return this;
    }

    public ClassGeneratorBuilder ConstructorDeclaration(int parameterCount)
    {
        _builder.AppendLine($@"
        public MyTestClass({GenerateOneLineParameters(parameterCount)})
        {{
        }}");

        return this;
    }

    public ClassGeneratorBuilder ConstructorDeclarationJaggered(int parameterCount)
    {
        _builder.AppendLine($@"
        public MyTestClass({GenerateJaggeredLineParameters(parameterCount)})
        {{
        }}");

        return this;
    }

    public ClassGeneratorBuilder ConstructorDeclarationStaggered(int parameterCount)
    {
        _builder.AppendLine($@"
        public MyTestClass({GenerateStaggeredLineParameters(parameterCount)})
        {{
        }}");

        return this;
    }

    public ClassGeneratorBuilder DelegateDeclaration(int parameterCount)
    {
        _builder.AppendLine($@"
        public delegate void DelegateDefinition({GenerateOneLineParameters(parameterCount)});");

        return this;
    }

    public ClassGeneratorBuilder DelegateDeclarationJaggered(int parameterCount)
    {
        _builder.AppendLine($@"
        public delegate void DelegateDefinition({GenerateJaggeredLineParameters(parameterCount)});");

        return this;
    }

    public ClassGeneratorBuilder DelegateDeclarationStaggered(int parameterCount)
    {
        _builder.AppendLine($@"
        public delegate void DelegateDefinition({GenerateStaggeredLineParameters(parameterCount)});");

        return this;
    }

    public ClassGeneratorBuilder AnonymousMethodExpression(int parameterCount)
    {
        _builder.AppendLine($@"
        public delegate void DelegateDefinition({GenerateOneLineParameters(parameterCount)});
        public void MyInnerMethod()
        {{
            DelegateDefinition action = delegate({GenerateOneLineParameters(parameterCount)})
            {{
            }};
        }}");

        return this;
    }

    public ClassGeneratorBuilder AnonymousMethodExpressionJaggered(int parameterCount)
    {
        _builder.AppendLine($@"
        public delegate void DelegateDefinition({GenerateOneLineParameters(parameterCount)});
        public void MyInnerMethod()
        {{
            DelegateDefinition action = delegate({GenerateJaggeredLineParameters(parameterCount)})
            {{
            }};
        }}");

        return this;
    }

    public ClassGeneratorBuilder AnonymousMethodExpressionStaggered(int parameterCount)
    {
        _builder.AppendLine($@"
        public delegate void DelegateDefinition({GenerateOneLineParameters(parameterCount)});
        public void MyInnerMethod()
        {{
            DelegateDefinition action = delegate({GenerateStaggeredLineParameters(parameterCount)})
            {{
            }};
        }}");

        return this;
    }

    public ClassGeneratorBuilder ParenthesizedLambdaExpression(int parameterCount)
    {
        _builder.AppendLine($@"
        public void MyInnerMethod()
        {{
            var action = ({GenerateOneLineParameters(parameterCount)}) =>
            {{
            }};
        }}");

        return this;
    }

    public ClassGeneratorBuilder ParenthesizedLambdaExpressionJaggered(int parameterCount)
    {
        _builder.AppendLine($@"
        public void MyInnerMethod()
        {{
            var action = ({GenerateJaggeredLineParameters(parameterCount)}) =>
            {{
            }};
        }}");

        return this;
    }

    public ClassGeneratorBuilder ParenthesizedLambdaExpressionStaggered(int parameterCount)
    {
        _builder.AppendLine($@"
        public void MyInnerMethod()
        {{
            var action = ({GenerateStaggeredLineParameters(parameterCount)}) =>
            {{
            }};
        }}");

        return this;
    }

    public ClassGeneratorBuilder IndexerDeclaration(int parameterCount)
    {
        _builder.AppendLine($@"
        public int this[{GenerateOneLineParameters(parameterCount)}] => default;");

        return this;
    }

    public ClassGeneratorBuilder IndexerDeclarationJaggered(int parameterCount)
    {
        _builder.AppendLine($@"
        public int this[{GenerateStaggeredLineParameters(parameterCount)}] => default;");

        return this;
    }

    public ClassGeneratorBuilder IndexerDeclarationStaggered(int parameterCount)
    {
        _builder.AppendLine($@"
        public int this[{GenerateStaggeredLineParameters(parameterCount)}] => default;");

        return this;
    }

    public ClassGeneratorBuilder InvocationExpression(int parameterCount)
    {
        MethodDeclaration(parameterCount);
        _builder.AppendLine($@"
        public void MyInnerMethod()
        {{
            MyMethod({GenerateOneLineArguments(parameterCount)});
        }}");

        return this;
    }

    public ClassGeneratorBuilder InvocationExpressionJaggered(int parameterCount)
    {
        MethodDeclaration(parameterCount);
        _builder.AppendLine($@"
        public void MyInnerMethod()
        {{
            MyMethod({GenerateJaggeredLineArguments(parameterCount)});
        }}");

        return this;
    }

    public ClassGeneratorBuilder InvocationExpressionStaggered(int parameterCount)
    {
        MethodDeclaration(parameterCount);
        _builder.AppendLine($@"
        public void MyInnerMethod()
        {{
            MyMethod({GenerateStaggeredLineArguments(parameterCount)});
        }}");

        return this;
    }

    public ClassGeneratorBuilder ObjectCreationExpression(int parameterCount)
    {
        _builder.AppendLine($@"
        public class MyInnerTest
        {{
            public MyInnerTest({GenerateOneLineParameters(parameterCount)})
            {{
            }}
        }}
    
        public void MyInnerMethod()
        {{
            var myInnerTest = new MyInnerTest({GenerateOneLineArguments(parameterCount)});
        }}");

        return this;
    }

    public ClassGeneratorBuilder ObjectCreationExpressionJaggered(int parameterCount)
    {
        _builder.AppendLine($@"
        public class MyInnerTest
        {{
            public MyInnerTest({GenerateOneLineParameters(parameterCount)})
            {{
            }}
        }}
    
        public void MyInnerMethod()
        {{
            var myInnerTest = new MyInnerTest({GenerateJaggeredLineArguments(parameterCount)});
        }}");

        return this;
    }

    public ClassGeneratorBuilder ObjectCreationExpressionStaggered(int parameterCount)
    {
        _builder.AppendLine($@"
        public class MyInnerTest
        {{
            public MyInnerTest({GenerateOneLineParameters(parameterCount)})
            {{
            }}
        }}
    
        public void MyInnerMethod()
        {{
            var myInnerTest = new MyInnerTest({GenerateStaggeredLineArguments(parameterCount)});
        }}");

        return this;
    }

    public ClassGeneratorBuilder Attribute(int parameterCount)
    {
        _builder.AppendLine($@"
        public class MyInnerTestAttribute : System.Attribute
        {{
            public MyInnerTestAttribute({GenerateOneLineParameters(parameterCount)})
            {{
            }}
        }}
    
        [MyInnerTest({GenerateOneLineArguments(parameterCount)})]
        public void MyInnerMethod()
        {{
        }}");

        return this;
    }

    public ClassGeneratorBuilder AttributeJaggered(int parameterCount)
    {
        _builder.AppendLine($@"
        public class MyInnerTestAttribute : System.Attribute
        {{
            public MyInnerTestAttribute({GenerateOneLineParameters(parameterCount)})
            {{
            }}
        }}
    
        [MyInnerTest({GenerateJaggeredLineArguments(parameterCount)})]
        public void MyInnerMethod()
        {{
        }}");

        return this;
    }

    public ClassGeneratorBuilder AttributeStaggered(int parameterCount)
    {
        _builder.AppendLine($@"
        public class MyInnerTestAttribute : System.Attribute
        {{
            public MyInnerTestAttribute({GenerateOneLineParameters(parameterCount)})
            {{
            }}
        }}
    
        [MyInnerTest({GenerateStaggeredLineArguments(parameterCount)})]
        public void MyInnerMethod()
        {{
        }}");

        return this;
    }

    public ClassGeneratorBuilder ElementAccessExpression(int parameterCount)
    {
        _builder.AppendLine($@"
        public void MyInnerMethod()
        {{
            var myArray = new int[{GenerateOneLineArguments(parameterCount)}];
            myArray[{GenerateOneLineArguments(parameterCount)}] = 1;
        }}");

        return this;
    }

    public ClassGeneratorBuilder ElementAccessExpressionJaggered(int parameterCount)
    {
        MethodDeclaration(parameterCount);
        _builder.AppendLine($@"
        public void MyInnerMethod()
        {{
            var myArray = new int[{GenerateOneLineArguments(parameterCount)}];
            myArray[{GenerateJaggeredLineArguments(parameterCount)}] = 1;
        }}");

        return this;
    }

    public ClassGeneratorBuilder ElementAccessExpressionStaggered(int parameterCount)
    {
        MethodDeclaration(parameterCount);
        _builder.AppendLine($@"
        public void MyInnerMethod()
        {{
            var myArray = new int[{GenerateOneLineArguments(parameterCount)}];
            myArray[{GenerateStaggeredLineArguments(parameterCount)}] = 1;
        }}");

        return this;
    }
    public string Generate()
    {
        _builder.AppendLine(@"    }
}");

        return _builder.ToString();
    }

    private static string GenerateOneLineParameters(int parameterCount)
    {
        return string.Join(", ", Enumerable.Range(0, parameterCount).Select(i => $"int a{i}"));
    }

    private static string GenerateJaggeredLineParameters(int parameterCount)
    {
        int halfWayPoint = parameterCount / 2;

        return $@"{string.Join(", ", Enumerable.Range(0, halfWayPoint).Select(i => $"int a{i}"))},
            {string.Join(", ", Enumerable.Range(halfWayPoint + 1, parameterCount).Select(i => $"int a{i}"))}";
    }

    private static string GenerateStaggeredLineParameters(int parameterCount)
    {
        var stringBuilder = new StringBuilder();

        for (int i = 0; i < parameterCount - 1; ++i)
        {
            stringBuilder.Append("            ").Append("int ").Append("a").Append(i).AppendLine(",");
        }

        stringBuilder.Append("           ").Append("int ").Append("a").Append(parameterCount - 1);

        return stringBuilder.ToString();
    }

    private static string GenerateOneLineArguments(int parameterCount)
    {
        return string.Join(", ", Enumerable.Range(0, parameterCount).Select(i => $"{i}"));
    }

    private static string GenerateJaggeredLineArguments(int parameterCount)
    {
        int halfWayPoint = parameterCount / 2;

        return $@"{string.Join(", ", Enumerable.Range(0, halfWayPoint).Select(i => $"{i}"))},
            {string.Join(", ", Enumerable.Range(halfWayPoint + 1, parameterCount).Select(i => $"{i}"))}";
    }

    private static string GenerateStaggeredLineArguments(int parameterCount)
    {
        var stringBuilder = new StringBuilder();

        for (int i = 0; i < parameterCount - 1; ++i)
        {
            stringBuilder.Append("            ").Append(i).AppendLine(",");
        }

        stringBuilder.Append("           ").Append(parameterCount - 1);

        return stringBuilder.ToString();
    }

}
