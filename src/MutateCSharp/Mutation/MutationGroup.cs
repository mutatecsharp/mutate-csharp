using System.Diagnostics;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MutateCSharp.Mutation;

public record ExprInfo(TypeInfo Type, SyntaxKind Operation);

public record MutationGroup
{
  public required ICollection<Mutation> Mutations { get; init; }
  public required string SchemaBaseName { get; init; }
  public required IList<ParameterSyntax> SchemaParameters { get; init; }
}