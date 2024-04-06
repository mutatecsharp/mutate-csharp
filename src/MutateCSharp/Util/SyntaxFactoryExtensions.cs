using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MutateCSharp.Util;

public static class SyntaxFactoryUtil
{
  public static InvocationExpressionSyntax CreateMethodCall(
    string namespaceName,
    string className,
    string methodName,
    params ExpressionSyntax[] arguments)
  {
    // Create the method identifier
    var memberIdentifier = SyntaxFactory.MemberAccessExpression(
      SyntaxKind.SimpleMemberAccessExpression,
      SyntaxFactory.IdentifierName(namespaceName),
      SyntaxFactory.IdentifierName(className)
    );

    var methodIdentifier = SyntaxFactory.MemberAccessExpression(
      SyntaxKind.SimpleMemberAccessExpression,
      memberIdentifier,
      SyntaxFactory.IdentifierName(methodName)
    );

    // Create the argument list
    var argumentList = SyntaxFactory.SeparatedList(
      arguments.Select(SyntaxFactory.Argument)
    );

    // Create the argument list syntax
    var argumentListSyntax = SyntaxFactory.ArgumentList(argumentList);

    // Return created method call expression
    return SyntaxFactory.InvocationExpression(methodIdentifier,
      argumentListSyntax);
  }
}