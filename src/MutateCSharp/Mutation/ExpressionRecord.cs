using Microsoft.CodeAnalysis.CSharp;
using MutateCSharp.Util;

namespace MutateCSharp.Mutation;

public sealed record ExpressionRecord(
  SyntaxKind Operation,
  CodeAnalysisUtil.OperandKind Operand,
  string ExpressionTemplate);