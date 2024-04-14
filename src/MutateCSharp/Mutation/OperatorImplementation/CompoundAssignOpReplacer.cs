using System.Collections.Frozen;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using MutateCSharp.Util;

namespace MutateCSharp.Mutation.OperatorImplementation;

/*
 * Compound assignment
 * x += y;
 * is equivalent to
 * x = x + y;
 *
 * Compound assignment operators cannot be overloaded;
 * its definition will be inferred from the corresponding
 * binary operator. (eg: += from +)
 */
public sealed partial class CompoundAssignOpReplacer(
  SemanticModel semanticModel) :
  AbstractMutationOperator<AssignmentExpressionSyntax>(semanticModel)
{
  // TODO: refactor into records
  private static readonly FrozenSet<SyntaxKind> supportedOperators
    = new HashSet<SyntaxKind>
    {
      SyntaxKind.AddAssignmentExpression,
      SyntaxKind.SubtractAssignmentExpression,
      SyntaxKind.MultiplyAssignmentExpression,
      SyntaxKind.DivideAssignmentExpression,
      SyntaxKind.ModuloAssignmentExpression,
      SyntaxKind.AndAssignmentExpression,
      SyntaxKind.ExclusiveOrAssignmentExpression,
      SyntaxKind.OrAssignmentExpression,
      SyntaxKind.LeftShiftAssignmentExpression,
      SyntaxKind.RightShiftAssignmentExpression,
      SyntaxKind.UnsignedRightShiftAssignmentExpression,
    }.ToFrozenSet();

  private static readonly FrozenDictionary<SyntaxKind, int> operatorIds
    = SyntaxKindUniqueIdGenerator.GenerateIds(supportedOperators).ToFrozenDictionary();
  
  // String types are not supported since it only supports += operator and cannot
  // be mutated
  private static readonly FrozenSet<SpecialType> PredefinedTypes =
    new HashSet<SpecialType>
    {
      // Boolean type
      SpecialType.System_Boolean,
      // Signed type(s)
      SpecialType.System_SByte,
      SpecialType.System_Int16,
      SpecialType.System_Int32,
      SpecialType.System_Int64,
      // Unsigned type(s)
      SpecialType.System_Byte,
      SpecialType.System_UInt16,
      SpecialType.System_UInt32,
      SpecialType.System_UInt64,
      // Floating point type(s)
      SpecialType.System_Single,
      SpecialType.System_Double,
      SpecialType.System_Decimal,
      // Integral type (neither signed or unsigned)
      SpecialType.System_Char,
    }.ToFrozenSet();

  /*
   * Roslyn Syntax API documentation for compound assignment operators:
   * https://learn.microsoft.com/en-us/dotnet/api/microsoft.codeanalysis.csharp.syntax.assignmentexpressionsyntax?view=roslyn-dotnet-4.7.0
   */
  private static readonly FrozenDictionary<SyntaxKind, string>
    SupportedArithmeticOperators =
      new Dictionary<SyntaxKind, string>
      {
        { SyntaxKind.AddAssignmentExpression, "+=" },
        { SyntaxKind.SubtractAssignmentExpression, "-=" },
        { SyntaxKind.MultiplyAssignmentExpression, "*=" },
        { SyntaxKind.DivideAssignmentExpression, "/=" },
        { SyntaxKind.ModuloAssignmentExpression, "%=" }
        // SyntaxKind.CoalesceAssignmentExpression // ??= (Supports reference or nullable types)
      }.ToFrozenDictionary();

  private static readonly FrozenDictionary<SyntaxKind, string>
    SupportedBitwiseOperators =
      new Dictionary<SyntaxKind, string>
      {
        { SyntaxKind.AndAssignmentExpression, "&=" },
        { SyntaxKind.ExclusiveOrAssignmentExpression, "^=" },
        { SyntaxKind.OrAssignmentExpression, "|=" },
        { SyntaxKind.LeftShiftAssignmentExpression, "<<=" },
        { SyntaxKind.RightShiftAssignmentExpression, ">>=" },
        { SyntaxKind.UnsignedRightShiftAssignmentExpression, ">>>=" }
      }.ToFrozenDictionary();

  private static readonly FrozenDictionary<SyntaxKind, string> SupportedBooleanOperators =
    new Dictionary<SyntaxKind, string>
    {
      {SyntaxKind.AndAssignmentExpression, "&="},
      {SyntaxKind.ExclusiveOrAssignmentExpression, "^="},
      {SyntaxKind.OrAssignmentExpression, "|="}
    }.ToFrozenDictionary();

  protected override bool CanBeApplied(AssignmentExpressionSyntax originalNode)
  {
    return supportedOperators.Contains(originalNode.Kind());
  }
  
  private static string ExpressionTemplate(SyntaxKind kind)
  {
    if (!SupportedArithmeticOperators.TryGetValue(kind, out var op)
        && !SupportedBitwiseOperators.TryGetValue(kind, out op)
        && !SupportedBooleanOperators.TryGetValue(kind, out op))
    {
      throw new NotSupportedException("Operator is not supported.");
    }

    return $"{{0}} {op} {{1}}";
  }

  protected override string OriginalExpressionTemplate(
    AssignmentExpressionSyntax originalNode)
  {
    return ExpressionTemplate(originalNode.Kind());
  }

  protected override IList<(int, string)> ValidMutantExpressionsTemplate(
    AssignmentExpressionSyntax originalNode)
  {
    var type = SemanticModel.GetTypeInfo(originalNode).Type!;
    var validOperators = new List<SyntaxKind>();
    
    // Case 1: Predefined types
    if (type.SpecialType is not SpecialType.None)
    {
      foreach (var op in supportedOperators)
      {
        if (op != originalNode.Kind() &&
            CanApplyReplacementOperatorForPredefinedTypes(type.SpecialType, op))
        {
          validOperators.Add(op);
        } 
      }
    }
    // Case 2: user-defined types
    else if (type is INamedTypeSymbol customType)
    {
      var candidateOverloadedOperators =
        CodeAnalysisUtil.GetOverloadedOperatorsInUserDefinedType(customType);

      foreach (var op in supportedOperators)
      {
        if (op != originalNode.Kind() &&
            CanApplyReplacementOperatorForUserDefinedTypes(
              candidateOverloadedOperators, originalNode, op)
           )
        {
          validOperators.Add(op);
        }
      }
    }

    // Assign id and sort to get unique ordering
    var idToOperators =
      SyntaxKindUniqueIdGenerator.ReturnSortedIdsToKind(operatorIds,
        validOperators);
    
    // Return expression templates
    return idToOperators
        .Select(entry => (entry.Item1, ExpressionTemplate(entry.Item2)))
        .ToList();
  }

  protected override IList<string> ParameterTypes(
    AssignmentExpressionSyntax originalNode)
  {
    var firstVariableType =
      SemanticModel.GetTypeInfo(originalNode.Left).Type;
    var secondVariableType =
      SemanticModel.GetTypeInfo(originalNode.Right).Type;
    return [$"ref {firstVariableType!.Name}", secondVariableType!.Name];
  }

  protected override string ReturnType(AssignmentExpressionSyntax originalNode)
  {
    return SemanticModel.GetTypeInfo(originalNode.Left).Type!.Name;
  }

  protected override string SchemaBaseName(
    AssignmentExpressionSyntax originalNode)
  {
    return "ReplaceCompoundAssignmentConstant";
  }
}

// Validation checks
public sealed partial class CompoundAssignOpReplacer
{
  private static bool CanApplyReplacementOperatorForPredefinedTypes(
    SpecialType type, SyntaxKind replacementOpKind)
  {
    if (PredefinedTypes.Contains(type))
    {
      return type switch
      {
        // Boolean type supports &=, ^=, |= operators
        SpecialType.System_Boolean
          =>
          SupportedBooleanOperators.ContainsKey(replacementOpKind),
        // Integral types support arithmetic and bitwise operators 
        SpecialType.System_Char
          or SpecialType.System_SByte
          or SpecialType.System_Int16
          or SpecialType.System_Int32
          or SpecialType.System_Int64
          or SpecialType.System_Byte
          or SpecialType.System_UInt16
          or SpecialType.System_UInt32
          or SpecialType.System_UInt64
          =>
          SupportedArithmeticOperators.ContainsKey(replacementOpKind) ||
          SupportedBitwiseOperators.ContainsKey(replacementOpKind),
        // Floating-point types support arithmetic operators
        SpecialType.System_Single
          or SpecialType.System_Double
          or SpecialType.System_Decimal
          =>
          SupportedArithmeticOperators.ContainsKey(replacementOpKind),
        _ => false
      };
    }

    return false; // Predefined type not supported
  }

  /*
   * In C# it is possible to define operator overloads for user-defined types
   * that return different types:
   *
   * class C {
   *   public static bool operator+(C first, C second) => true;
   * }
   *
   * is a valid definition, but this means the following code:
   * var c1 = new C();
   * var c2 = new C();
   * c1 += c2; // will not compile, since c1 + c2 returns bool and cannot be cast to C
   *
   * as such, we need to infer the type if it can be assignable to C before replacing
   * the operator.
   *
   * assuming the following statement:
   * a op1 b;
   * where:
   * a is of type A or A?,
   * b is of type B or B?,
   * op1 is the original compound assignment operator,
   * and a replacement operator op2 is proposed,
   * the following should hold:
   *
   * 1) op2 should exist in A, or should exist in A's base type;
   * 2) op2 should take two parameters of type A/A? and B/B? respectively;
   * 3) op2 should return type A;
   */
  // todo: handle inherited operators that are applicable
  // todo: handle case where the variable is not the same type as the
  // parameter type but variable type can be assigned to the parameter type
  // as it either inherits the class or implements the interface
  private bool CanApplyReplacementOperatorForUserDefinedTypes(
    IDictionary<SyntaxKind, IMethodSymbol> overloadedOperators,
    AssignmentExpressionSyntax originalNode,
    SyntaxKind replacementOpKind
  )
  {
    // 1) replacement operator should have an existing definition in A
    if (!overloadedOperators.TryGetValue(replacementOpKind, out var opMethod))
      return false;

    // 2) Get the parameter types for the replacement operator
    if (opMethod.Parameters is not
        [var firstParam, var secondParam]) return false;

    // 3) Get the types of variables involved in the compound assignment operator
    var firstVariableType =
      SemanticModel.GetTypeInfo(originalNode.Left).Type;
    var secondVariableType =
      SemanticModel.GetTypeInfo(originalNode.Right).Type;

    // 4) Check that the types match:
    // First parameter
    var checkFirstTypeMatches = firstVariableType?.Equals(firstParam.Type,
      SymbolEqualityComparer.Default) ?? false;

    if (!checkFirstTypeMatches) return false;

    // Second parameter
    var checkSecondTypeMatches =
      secondVariableType?.Equals(secondParam.Type,
        SymbolEqualityComparer.Default) ?? false;

    if (!checkSecondTypeMatches) return false;

    // Result (Given a += b and -= as replacement, -= should return the same
    // type as +=, which is type representing a)
    var checkResultTypeMatches =
      opMethod.ReturnType.Equals(firstVariableType,
        SymbolEqualityComparer.Default);

    return checkResultTypeMatches;
  }
}