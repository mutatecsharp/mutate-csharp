using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using MutateCSharp.Util;

namespace MutateCSharp.Mutation;

public class MutantSchemataGenerator
{
  private static string _namespaceName = "MutateCSharp";
  private static string _className = "Schemata";

  private readonly IDictionary<string, int>
    _schemaBasenameCounter = new Dictionary<string, int>();

  private IDictionary<SchemaGroup, string>
    _schemaNames = new Dictionary<SchemaGroup, string>();

  // Many-to-one mapping
  private IDictionary<MutationGroup, SchemaGroup>
    _mutationGroupToSchemaGroup = new Dictionary<MutationGroup, SchemaGroup>();
  
  public string GenerateSchemaName(MutationGroup mutationGroup)
  {
    var schemaGroup = RegisterSchemaGroup(mutationGroup);

    // Return generated schema name if schema group already exists
    if (_schemaNames.TryGetValue(schemaGroup, out var name)) return name;

    // Append counter as suffix to schema base name, then increment counter
    var newName =
      $"{mutationGroup.SchemaBaseName}{_schemaBasenameCounter[mutationGroup.SchemaBaseName]}";
    _schemaBasenameCounter[mutationGroup.SchemaBaseName]++;
    return newName;
  }

  private SchemaGroup RegisterSchemaGroup(MutationGroup mutationGroup)
  {
    var operandType = mutationGroup.Mutations.First().OperandType;
    var originalOperation = mutationGroup.Mutations.First().OriginalOperation;
    var mutantOperations =
      mutationGroup.Mutations.Select(m => m.MutantOperation).ToArray();
    var schemaGroup = new SchemaGroup
    {
      OperandType = operandType,
      OriginalOperation = originalOperation,
      MutantOperations = mutantOperations
    };

    _mutationGroupToSchemaGroup[mutationGroup] = schemaGroup;
    return schemaGroup;
  }

  private ParameterListSyntax GenerateSchemaParameters(SchemaGroup schemaGroup)
  {
    // Hack: type is predefined (assumption)
    var type =
      SyntaxFactory.PredefinedType(
        SyntaxFactory.ParseToken(schemaGroup.OperandType));
      
    // Parameter: int mutantId 
    var mutantIdParameter = SyntaxFactory
      .Parameter(SyntaxFactory.Identifier("mutantId"))
      .WithType(
        SyntaxFactory.PredefinedType(
          SyntaxFactory.Token(SyntaxKind.IntKeyword)));
    // Hack: unary parameter = <type> argument
    var parameter = SyntaxFactoryUtil.CreatePredefinedUnaryParameters(schemaGroup.OperandType);

    // Parameters: int mutantId, ... arguments
    return SyntaxFactory.ParameterList(
      SyntaxFactory.SeparatedList(
        ImmutableArray.Create(mutantIdParameter, parameter))
    );
  }

  // Hack: unary operator result
  // Returns "if (_activatedMutantId == mutantId + i) operator(... operand)"
  private BlockSyntax GenerateSchemaCases(MutationGroup mutationGroup)
  {
    var schemaGroup = _mutationGroupToSchemaGroup[mutationGroup];
    
    var cases = mutationGroup.Mutations.Select((mutantOp, i) =>
    {
      // mutantId + i
      var caseIdExpr = 
        SyntaxFactory.BinaryExpression(SyntaxKind.AddExpression,
        SyntaxFactory.IdentifierName("mutantId"),
        SyntaxFactory.LiteralExpression(SyntaxKind.NumericLiteralExpression,
          SyntaxFactory.Literal(i)));
      // _activatedMutantId == mutantId + i
      var matchExpr =
        SyntaxFactory.BinaryExpression(SyntaxKind.EqualsExpression,
          SyntaxFactory.IdentifierName("_activatedMutantId"),
          caseIdExpr);
        
      // if (_activatedMutantId == mutantId + i) return operator(... operand);
      return SyntaxFactory.IfStatement(matchExpr,
        SyntaxFactory.ReturnStatement(mutantOp.RoslynReplacementNode));
    });

    var defaultCase = SyntaxFactory.ReturnStatement(mutationGroup.Mutations.First().RoslynOriginalNode);
    var statements = cases.Append<StatementSyntax>(defaultCase);
    return SyntaxFactory.Block(statements);
  }

  private MethodDeclarationSyntax GenerateIndividualSchema(
    MutationGroup mutationGroup)
  {
    var schemaGroup = _mutationGroupToSchemaGroup[mutationGroup];
    
    // Type: assume input = output type
    var type =
      SyntaxFactory.PredefinedType(
        SyntaxFactory.ParseToken(schemaGroup.OperandType));
    
    // Modifier: public static
    var modifiers = SyntaxFactory.TokenList([
      SyntaxFactory.Token(SyntaxKind.PublicKeyword),
      SyntaxFactory.Token(SyntaxKind.StaticKeyword)
    ]);
    
    return SyntaxFactory.MethodDeclaration(returnType: type,
      identifier: _schemaNames[schemaGroup])
      .WithModifiers(modifiers)
      .WithParameterList(GenerateSchemaParameters(schemaGroup))
      .WithBody(GenerateSchemaCases(mutationGroup));
  }

  
  private static SyntaxTree GenerateInitialiseMethod()
  {
    return CSharpSyntaxTree.ParseText(
      """
      private static void Initialise()
      {
        if (_initialised) return;
        var activatedMutant = Environment.GetEnvironmentVariable("MUTATE_CSHARP_ACTIVATED_MUTANT");
        if (!string.IsNullOrEmpty(activatedMutant)) _activatedMutantId = int.Parse(activatedMutant);
        _initialised = true;
      }

      private static bool ActivatedInRange(int lowerBound, int upperBound)
      {
        Initialise();
        return lowerBound <= _activatedMutantId && _activatedMutantId <= upperBound;
      }
      """
    );
  }

  // public SyntaxTree GenerateSchemata()
  // {
  // }


  // // Use a document editor here
  // public SyntaxTree InjectSchemata(SyntaxTree tree)
  // {
  // }


  private record SchemaGroup
  {
    public required string OperandType { get; init; }
    public required SyntaxKind OriginalOperation { get; init; }
    public required IList<SyntaxKind> MutantOperations { get; init; }
  }
}