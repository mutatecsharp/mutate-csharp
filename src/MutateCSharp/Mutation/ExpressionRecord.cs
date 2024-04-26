using Microsoft.CodeAnalysis.CSharp;

namespace MutateCSharp.Mutation;

public sealed record ExpressionRecord(
  SyntaxKind Operation,
  string ExpressionTemplate);