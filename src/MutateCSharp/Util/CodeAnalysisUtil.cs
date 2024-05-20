using System.Collections.Frozen;
using System.Collections.Immutable;
using System.Globalization;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MutateCSharp.Util;

/*
 * Definitions.
 */
public static partial class CodeAnalysisUtil
{
  [Flags]
  public enum SupportedType
  {
    UnsignedIntegral = 1 << 0,
    SignedIntegral = 1 << 1,
    FloatingPoint = 1 << 2,
    Boolean = 1 << 3,
    Character = 1 << 4,
    String = 1 << 5,
    NotSupported = 1 << 6,
    Integral = UnsignedIntegral | SignedIntegral,
    Numeric = Integral | FloatingPoint,
    All = Integral | FloatingPoint | Boolean | Character | String
  }

  public record MethodSignature(
    ITypeSymbol ReturnType,
    ImmutableArray<ITypeSymbol> OperandTypes);

  public record Op(
    SyntaxKind ExprKind,
    SyntaxKind TokenKind,
    string MemberName)
  {
    public override string ToString()
    {
      return SyntaxFacts.GetText(TokenKind);
    }
  }

  private const SupportedType ArithmeticOrRelationalOperandTypeClass = 
    SupportedType.Numeric | SupportedType.Character;

  private const SupportedType BitwiseNumericOperandTypeClass = 
    SupportedType.Integral | SupportedType.Character;

  public static readonly FrozenSet<SyntaxKind> ShortCircuitOperators
    = new HashSet<SyntaxKind>
    {
      // Binary operators
      SyntaxKind.LogicalAndExpression,
      SyntaxKind.LogicalOrExpression,
      SyntaxKind.CoalesceExpression,
      SyntaxKind.ConditionalExpression
    }.ToFrozenSet();

  public static readonly FrozenSet<SyntaxKind> VariableModifyingOperators
    = new HashSet<SyntaxKind>
    {
      // Unary operators
      SyntaxKind.PreIncrementExpression,
      SyntaxKind.PostIncrementExpression,
      SyntaxKind.PreDecrementExpression,
      SyntaxKind.PostDecrementExpression,
      // Binary operators
      SyntaxKind.AddAssignmentExpression,
      SyntaxKind.SubtractAssignmentExpression,
      SyntaxKind.MultiplyAssignmentExpression,
      SyntaxKind.DivideAssignmentExpression,
      SyntaxKind.ModuloAssignmentExpression,
      SyntaxKind.AndAssignmentExpression,
      SyntaxKind.OrAssignmentExpression,
      SyntaxKind.ExclusiveOrAssignmentExpression,
      SyntaxKind.LeftShiftAssignmentExpression,
      SyntaxKind.RightShiftAssignmentExpression,
      SyntaxKind.UnsignedRightShiftAssignmentExpression
    }.ToFrozenSet();

  public enum SymbolVisibility
  {
    Private,
    Public,
    Internal
  }
}

/*
 * Helper methods.
 */
public static partial class CodeAnalysisUtil
{
  public static SupportedType GetSpecialTypeClassification(SpecialType type)
  {
    return type switch
    {
      SpecialType.System_Boolean
        => SupportedType.Boolean,
      SpecialType.System_Char // char type is integral in C#
        or SpecialType.System_SByte
        or SpecialType.System_Int16
        or SpecialType.System_Int32
        or SpecialType.System_Int64
        => SupportedType.SignedIntegral,
      SpecialType.System_Byte
        or SpecialType.System_UInt16
        or SpecialType.System_UInt32
        or SpecialType.System_UInt64
        => SupportedType.UnsignedIntegral,
      SpecialType.System_Single
        or SpecialType.System_Double
        or SpecialType.System_Decimal
        => SupportedType.FloatingPoint,
      SpecialType.System_String
        => SupportedType.String,
      _ => SupportedType.NotSupported
    };
  }

  public static FrozenDictionary<SyntaxKind,
      ImmutableArray<MethodSignature>>
    BuildBinaryNumericOperatorMethodSignature(this SemanticModel model)
  {
    var dictionary = new Dictionary<SyntaxKind, ImmutableArray<MethodSignature>>();

    SpecialType[] smallerThanWordIntegralTypes =
    [
      SpecialType.System_SByte,
      SpecialType.System_Byte,
      SpecialType.System_Int16,
      SpecialType.System_UInt16,
      SpecialType.System_Char,
    ];

    SpecialType[] integralTypes =
    [
      SpecialType.System_Int32,
      SpecialType.System_UInt32,
      SpecialType.System_Int64,
      SpecialType.System_UInt64
    ];

    SpecialType[] arithmeticTypes =
    [
      SpecialType.System_Int32,
      SpecialType.System_UInt32,
      SpecialType.System_Int64,
      SpecialType.System_UInt64,
      SpecialType.System_Single,
      SpecialType.System_Double,
      SpecialType.System_Decimal
    ];

    SpecialType[] allSupportedTypes =
      arithmeticTypes
        .Concat([SpecialType.System_Boolean, SpecialType.System_String])
        .ToArray();

    // Arithmetic
    foreach (var operatorKind in
             new[]
             {
               SyntaxKind.AddExpression,
               SyntaxKind.SubtractExpression,
               SyntaxKind.MultiplyExpression,
               SyntaxKind.DivideExpression,
               SyntaxKind.ModuloExpression
             })
    {
      var typeSymbols = arithmeticTypes
        .Select(type => model.Compilation.GetSpecialType(type))
        .ToList();
        
      // Original form
      var exactMethodSignatures = typeSymbols
        .Select(typeSymbol =>
          new MethodSignature(typeSymbol, [typeSymbol, typeSymbol]));
      
      // Lifted form (both return / operand types are nullable)
      // For the binary operators +, -, *, /, %, &, |, ^, <<, and >>,
      // a lifted form of an operator exists if the operand and result types
      // are all non-nullable value types. The lifted form is constructed by
      // adding a single ? modifier to each operand and result type.
      var liftedMethodSignatures = typeSymbols
        .Select(model.ConstructNullableValueTypeSymbol)
        .Select(typeSymbol =>
          new MethodSignature(typeSymbol, [typeSymbol, typeSymbol]));

      dictionary[operatorKind] = [
        ..exactMethodSignatures
          .Concat(liftedMethodSignatures)
      ];
    }

    // Compound arithmetic
    foreach (var operatorKind in new[]
             {
               SyntaxKind.AddAssignmentExpression,
               SyntaxKind.SubtractAssignmentExpression,
               SyntaxKind.MultiplyAssignmentExpression,
               SyntaxKind.DivideAssignmentExpression,
               SyntaxKind.ModuloAssignmentExpression
             })
    {
      var typeSymbols = 
        smallerThanWordIntegralTypes.Concat(arithmeticTypes)
        .Select(type => model.Compilation.GetSpecialType(type))
        .ToList();
        
      // Original form
      var exactMethodSignatures = typeSymbols
        .Select(typeSymbol =>
          new MethodSignature(typeSymbol, [typeSymbol, typeSymbol]));
      
      // Lifted form (both return / operand types are nullable)
      // For the binary operators +, -, *, /, %, &, |, ^, <<, and >>,
      // a lifted form of an operator exists if the operand and result types
      // are all non-nullable value types. The lifted form is constructed by
      // adding a single ? modifier to each operand and result type.
      var liftedMethodSignatures = typeSymbols
        .Select(model.ConstructNullableValueTypeSymbol)
        .Select(typeSymbol =>
          new MethodSignature(typeSymbol, [typeSymbol, typeSymbol]));

      dictionary[operatorKind] = exactMethodSignatures
        .Concat(liftedMethodSignatures).ToImmutableArray();
    }

    // Relational
    foreach (var operatorKind in new[]
             {
               SyntaxKind.GreaterThanExpression,
               SyntaxKind.LessThanExpression,
               SyntaxKind.GreaterThanOrEqualExpression,
               SyntaxKind.LessThanOrEqualExpression
             })
    {
      var boolTypeSymbol =
        model.Compilation.GetSpecialType(SpecialType.System_Boolean);
      
      var typeSymbols = arithmeticTypes
        .Select(type => model.Compilation.GetSpecialType(type))
        .ToList();
      
      // Original form
      var exactMethodSignatures = typeSymbols
        .Select(typeSymbol =>
          new MethodSignature(boolTypeSymbol, [typeSymbol, typeSymbol]));
      
      // Lifted form (both return / operand types are nullable)
      // For the relational operators <, >, <=, and >=, a lifted form of an
      // operator exists if the operand types are both non-nullable value types
      // and if the result type is bool. The lifted form is constructed by
      // adding a single ? modifier to each operand type.
      var liftedMethodSignatures = typeSymbols
        .Select(model.ConstructNullableValueTypeSymbol)
        .Select(typeSymbol =>
          new MethodSignature(boolTypeSymbol, [typeSymbol, typeSymbol]));
      
      dictionary[operatorKind] = exactMethodSignatures
        .Concat(liftedMethodSignatures).ToImmutableArray();
    }

    // Equality
    foreach (var operatorKind in new[]
             {
               SyntaxKind.EqualsExpression,
               SyntaxKind.NotEqualsExpression
             })
    {
      var boolTypeSymbol =
        model.Compilation.GetSpecialType(SpecialType.System_Boolean);
      
      var typeSymbols = allSupportedTypes
          .Select(type => model.Compilation.GetSpecialType(type))
          .ToList();
        
      // Original form
      var exactMethodSignatures = typeSymbols
        .Select(typeSymbol =>
          new MethodSignature(boolTypeSymbol, [typeSymbol, typeSymbol]));
      
      // Lifted form (both return / operand types are nullable)
      // For the equality operators == and !=, a lifted form of an operator
      // exists if the operand types are both non-nullable value types and if
      // the result type is bool. The lifted form is constructed by adding a
      // single ? modifier to each operand type. 
      var liftedMethodSignatures = typeSymbols
        .Select(model.ConstructNullableValueTypeSymbol)
        .Select(typeSymbol =>
          new MethodSignature(boolTypeSymbol, [typeSymbol, typeSymbol]));

      dictionary[operatorKind] = exactMethodSignatures
        .Concat(liftedMethodSignatures).ToImmutableArray();
    }

    // Bitwise shift
    foreach (var operatorKind in new[]
             {
               SyntaxKind.LeftShiftExpression,
               SyntaxKind.RightShiftExpression,
               SyntaxKind.UnsignedRightShiftExpression
             })
    {
      // RHS is always int
      var intTypeSymbol =
        model.Compilation.GetSpecialType(SpecialType.System_Int32);
      var nullableIntTypeSymbol =
        model.ConstructNullableValueTypeSymbol(intTypeSymbol);
      
      var typeSymbols = 
        integralTypes
          .Select(type => model.Compilation.GetSpecialType(type))
          .ToList();
        
      // Original form
      var exactMethodSignatures = typeSymbols
        .Select(typeSymbol =>
          new MethodSignature(typeSymbol, [typeSymbol, intTypeSymbol]));
      
      // Lifted form (both return / operand types are nullable)
      // For the binary operators +, -, *, /, %, &, |, ^, <<, and >>,
      // a lifted form of an operator exists if the operand and result types
      // are all non-nullable value types. The lifted form is constructed by
      // adding a single ? modifier to each operand and result type.
      var liftedMethodSignatures = typeSymbols
        .Select(model.ConstructNullableValueTypeSymbol)
        .Select(typeSymbol =>
          new MethodSignature(typeSymbol, [typeSymbol, nullableIntTypeSymbol]));

      dictionary[operatorKind] = exactMethodSignatures
        .Concat(liftedMethodSignatures).ToImmutableArray();
    }

    // Compound bitwise shift 
    foreach (var operatorKind in new[]
             {
               SyntaxKind.LeftShiftAssignmentExpression,
               SyntaxKind.RightShiftAssignmentExpression,
               SyntaxKind.UnsignedRightShiftAssignmentExpression
             })
    {
      // RHS is always int
      var intTypeSymbol =
        model.Compilation.GetSpecialType(SpecialType.System_Int32);
      var nullableIntTypeSymbol =
        model.ConstructNullableValueTypeSymbol(intTypeSymbol);
      
      var typeSymbols = 
        smallerThanWordIntegralTypes.Concat(integralTypes)
          .Select(type => model.Compilation.GetSpecialType(type))
          .ToList();
        
      // Original form
      var exactMethodSignatures = typeSymbols
        .Select(typeSymbol =>
          new MethodSignature(typeSymbol, [typeSymbol, intTypeSymbol]));
      
      // Lifted form (both return / operand types are nullable)
      // For the binary operators +, -, *, /, %, &, |, ^, <<, and >>,
      // a lifted form of an operator exists if the operand and result types
      // are all non-nullable value types. The lifted form is constructed by
      // adding a single ? modifier to each operand and result type.
      var liftedMethodSignatures = typeSymbols
        .Select(model.ConstructNullableValueTypeSymbol)
        .Select(typeSymbol =>
          new MethodSignature(typeSymbol, [typeSymbol, nullableIntTypeSymbol]));

      dictionary[operatorKind] = exactMethodSignatures
        .Concat(liftedMethodSignatures).ToImmutableArray();
    }

    // (Bitwise / Boolean) Logical
    foreach (var operatorKind in new[]
             {
               SyntaxKind.BitwiseAndExpression,
               SyntaxKind.BitwiseOrExpression,
               SyntaxKind.ExclusiveOrExpression
             })
    {
      var typeSymbols = 
        integralTypes.Concat([SpecialType.System_Boolean])
        .Select(type => model.Compilation.GetSpecialType(type))
        .ToList();
        
      // Original form
      var exactMethodSignatures = typeSymbols
        .Select(typeSymbol =>
          new MethodSignature(typeSymbol, [typeSymbol, typeSymbol]));
      
      // Lifted form (both return / operand types are nullable)
      // For the binary operators +, -, *, /, %, &, |, ^, <<, and >>,
      // a lifted form of an operator exists if the operand and result types
      // are all non-nullable value types. The lifted form is constructed by
      // adding a single ? modifier to each operand and result type.
      var liftedMethodSignatures = typeSymbols
        .Select(model.ConstructNullableValueTypeSymbol)
        .Select(typeSymbol =>
          new MethodSignature(typeSymbol, [typeSymbol, typeSymbol]));

      dictionary[operatorKind] = exactMethodSignatures
        .Concat(liftedMethodSignatures).ToImmutableArray();
    }

    // Compound (bitwise / boolean) logical
    foreach (var operatorKind in new[]
             {
               SyntaxKind.AndAssignmentExpression,
               SyntaxKind.OrAssignmentExpression,
               SyntaxKind.ExclusiveOrAssignmentExpression
             })
    {
      var typeSymbols = 
        smallerThanWordIntegralTypes.Concat(integralTypes)
          .Concat([SpecialType.System_Boolean])
          .Select(type => model.Compilation.GetSpecialType(type))
          .ToList();
        
      // Original form
      var exactMethodSignatures = typeSymbols
        .Select(typeSymbol =>
          new MethodSignature(typeSymbol, [typeSymbol, typeSymbol]));
      
      // Lifted form (both return / operand types are nullable)
      // For the binary operators +, -, *, /, %, &, |, ^, <<, and >>,
      // a lifted form of an operator exists if the operand and result types
      // are all non-nullable value types. The lifted form is constructed by
      // adding a single ? modifier to each operand and result type.
      var liftedMethodSignatures = typeSymbols
        .Select(model.ConstructNullableValueTypeSymbol)
        .Select(typeSymbol =>
          new MethodSignature(typeSymbol, [typeSymbol, typeSymbol]));

      dictionary[operatorKind] = exactMethodSignatures
        .Concat(liftedMethodSignatures).ToImmutableArray();
    }

    // Conditional logical
    foreach (var operatorKind in new[]
             {
               SyntaxKind.LogicalAndExpression, 
               SyntaxKind.LogicalOrExpression
             })
    {
      var boolType =
        model.Compilation.GetSpecialType(SpecialType.System_Boolean);

      dictionary[operatorKind] =
        [new MethodSignature(boolType, [boolType, boolType])];
    }

    return dictionary.ToFrozenDictionary();
  }
  
  public static FrozenDictionary<SyntaxKind,
      ImmutableArray<MethodSignature>>
    BuildUnaryNumericOperatorMethodSignature(this SemanticModel model)
  {
    var dictionary = new Dictionary<SyntaxKind, ImmutableArray<MethodSignature>>();

    SpecialType[] smallerThanWordIntegralTypes =
    [
      SpecialType.System_SByte,
      SpecialType.System_Byte,
      SpecialType.System_Int16,
      SpecialType.System_UInt16,
      SpecialType.System_Char,
    ];

    SpecialType[] integralTypes =
    [
      SpecialType.System_Int32,
      SpecialType.System_UInt32,
      SpecialType.System_Int64,
      SpecialType.System_UInt64
    ];

    SpecialType[] floatingPointTypes =
    [
      SpecialType.System_Single,
      SpecialType.System_Double,
      SpecialType.System_Decimal
    ];

    SpecialType[] arithmeticTypes =
    [
      SpecialType.System_Int32,
      SpecialType.System_UInt32,
      SpecialType.System_Int64,
      SpecialType.System_UInt64,
      SpecialType.System_Single,
      SpecialType.System_Double,
      SpecialType.System_Decimal
    ];
    
    // For the unary operators +, ++, -, --, !, and ~, a lifted form of an
    // operator exists if the operand and result types are both non-nullable
    // value types. The lifted form is constructed by adding a single ?
    // modifier to the operand and result types.
    
    // Arithmetic (+)
    var plusTypeSymbols = arithmeticTypes
      .Select(type => model.Compilation.GetSpecialType(type)).ToArray();
    
    // Original form
    var plusExactTypeSignatures = plusTypeSymbols.Select(typeSymbol =>
      new MethodSignature(typeSymbol, [typeSymbol]));
    // Lifted form
    var plusLiftedMethodSignatures = plusTypeSymbols
      .Select(model.ConstructNullableValueTypeSymbol)
      .Select(typeSymbol => new MethodSignature(typeSymbol, [typeSymbol]));

    dictionary[SyntaxKind.UnaryPlusExpression] =
      [..plusExactTypeSignatures.Concat(plusLiftedMethodSignatures)];
    
    // Arithmetic (-)
    var minusTypeSymbols = 
      new[] { SpecialType.System_Int32, SpecialType.System_Int64 }
        .Concat(floatingPointTypes)
      .Select(type => model.Compilation.GetSpecialType(type)).ToArray();
    
    // Original form
    var minusExactTypeSignatures = minusTypeSymbols.Select(typeSymbol =>
      new MethodSignature(typeSymbol, [typeSymbol]));
    // Lifted form
    var minusLiftedMethodSignatures = minusTypeSymbols
      .Select(model.ConstructNullableValueTypeSymbol)
      .Select(typeSymbol => new MethodSignature(typeSymbol, [typeSymbol]));
    
    dictionary[SyntaxKind.UnaryMinusExpression] =
      [..minusExactTypeSignatures.Concat(minusLiftedMethodSignatures)];
    
    // Logical (!)
    var boolType = model.Compilation.GetSpecialType(SpecialType.System_Boolean);
    var nullableBoolType = model.ConstructNullableValueTypeSymbol(boolType);
    
    dictionary[SyntaxKind.LogicalNotExpression]
      =
      [
        new MethodSignature(boolType, [boolType]),
        new MethodSignature(nullableBoolType, [nullableBoolType])
      ];
    
    // Bitwise complement (~)
    var bitwiseTypeSymbols = 
      integralTypes.Select(type => model.Compilation.GetSpecialType(type)).ToArray();
    
    // Original form
    var bitwiseExactTypeSignatures = bitwiseTypeSymbols.Select(typeSymbol =>
      new MethodSignature(typeSymbol, [typeSymbol]));
    // Lifted form
    var bitwiseLiftedMethodSignatures = bitwiseTypeSymbols
      .Select(model.ConstructNullableValueTypeSymbol)
      .Select(typeSymbol => new MethodSignature(typeSymbol, [typeSymbol]));
    
    dictionary[SyntaxKind.BitwiseNotExpression] =
      [..bitwiseExactTypeSignatures.Concat(bitwiseLiftedMethodSignatures)];
    
    // Increment/decrement
    foreach (var operatorKind in new[]
             {
               SyntaxKind.PreIncrementExpression,
               SyntaxKind.PostIncrementExpression,
               SyntaxKind.PreDecrementExpression,
               SyntaxKind.PostDecrementExpression
             })
    {
      var typeSymbols = 
        smallerThanWordIntegralTypes.Concat(arithmeticTypes)
          .Select(type => model.Compilation.GetSpecialType(type))
          .ToList();
      
      // Original form
      var updateExactTypeSignatures = typeSymbols.Select(typeSymbol =>
        new MethodSignature(typeSymbol, [typeSymbol]));
      // Lifted form
      var updateLiftedMethodSignatures = typeSymbols
        .Select(model.ConstructNullableValueTypeSymbol)
        .Select(typeSymbol => new MethodSignature(typeSymbol, [typeSymbol]));
      
      dictionary[operatorKind] =
        [..updateExactTypeSignatures.Concat(updateLiftedMethodSignatures)];
    }

    return dictionary.ToFrozenDictionary();
  }

  /*
   * https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/language-specification/expressions#12472-unary-numeric-promotions
   * Implements the unary numeric promotion rule for +, -, ~.
   */
  public static ITypeSymbol? ResolveUnaryPrimitiveReturnType(
    this SemanticModel model, SpecialType operandType, SyntaxKind opKind)
  {
    return model.ResolveUnaryPrimitiveOperandType(operandType, opKind);
  }

  private static ITypeSymbol? ResolveUnaryPrimitiveOperandType(
    this SemanticModel model, SpecialType operandType, SyntaxKind opKind)
  {
    if (operandType is SpecialType.None) return null;
    
    if (opKind is SyntaxKind.UnaryMinusExpression
        or SyntaxKind.UnaryPlusExpression
        or SyntaxKind.PreIncrementExpression
        or SyntaxKind.PostIncrementExpression
        or SyntaxKind.PreDecrementExpression
        or SyntaxKind.PostDecrementExpression)
    {
      if (opKind is SyntaxKind.UnaryMinusExpression)
      {
        if (operandType is SpecialType.System_UInt64) return null;
        if (operandType is SpecialType.System_UInt32)
        {
          return model.Compilation.GetSpecialType(SpecialType.System_Int64);
        }
      }

      return ArithmeticOrRelationalOperandTypeClass.HasFlag(
        GetSpecialTypeClassification(operandType))
        ? model.Compilation.GetSpecialType(
          UnaryNumericPromotionType(operandType))
        : null;
    }

    if (opKind is SyntaxKind.BitwiseNotExpression)
    {
      return BitwiseNumericOperandTypeClass.HasFlag(
        GetSpecialTypeClassification(operandType))
        ? model.Compilation.GetSpecialType(
          UnaryNumericPromotionType(operandType))
        : null;
    }

    if (opKind is SyntaxKind.LogicalNotExpression)
    {
      return operandType is SpecialType.System_Boolean
        ? model.Compilation.GetSpecialType(operandType)
        : null;
    }

    return null;
  }

  private static SpecialType UnaryNumericPromotionType(SpecialType operandType)
  {
    if (operandType is SpecialType.System_Char or
        SpecialType.System_SByte or
        SpecialType.System_Byte or
        SpecialType.System_Int16 or
        SpecialType.System_UInt16)
    {
      return SpecialType.System_Int32;
    }

    return operandType;
  }

  public static SpecialType BinaryNumericPromotionType(SpecialType leftType,
    SpecialType rightType)
  {
    SpecialType[] types = [leftType, rightType];

    if (types.Any(type => type is SpecialType.System_Decimal))
    {
      // Compile-time error occurs if one operand is decimal but the other
      // is double or float
      return types.Any(type =>
        type is SpecialType.System_Double or SpecialType.System_Single)
        ? SpecialType.None
        : SpecialType.System_Decimal;
    }

    if (types.Any(type => type is SpecialType.System_Double))
    {
      return SpecialType.System_Double;
    }

    if (types.Any(type => type is SpecialType.System_Single))
    {
      return SpecialType.System_Single;
    }

    if (types.Any(type => type is SpecialType.System_UInt64))
    {
      // Compile-time error occurs if one operand is ulong but the other
      // is one of sbyte, short, int, or long.
      return types.Any(type => type
        is SpecialType.System_SByte
        or SpecialType.System_Int16
        or SpecialType.System_Int32
        or SpecialType.System_Int64)
        ? SpecialType.None
        : SpecialType.System_UInt64;
    }

    if (types.Any(type => type is SpecialType.System_Int64))
    {
      return SpecialType.System_Int64;
    }

    if (types.Any(type => type is SpecialType.System_UInt32))
    {
      return
        types.Any(type => type is SpecialType.System_SByte
          or SpecialType.System_Int16 or SpecialType.System_Int32)
          ? SpecialType.System_Int64
          : SpecialType.System_UInt32;
    }

    return SpecialType.System_Int32;
  }

  public static ITypeSymbol? ResolveTypeSymbol(
    this SemanticModel model, SyntaxNode node)
  {
    // Resolve null as 'object' type; return type otherwise
    return node.IsKind(SyntaxKind.NullLiteralExpression)
      ? model.Compilation.GetSpecialType(SpecialType.System_Object)
      : model.GetTypeInfo(node).Type;
  }

  public static ITypeSymbol? ResolveConvertedTypeSymbol(
    this SemanticModel model, SyntaxNode node)
  {
    // Resolve null as 'object' type; return converted type otherwise
    return node.IsKind(SyntaxKind.NullLiteralExpression)
      ? model.Compilation.GetSpecialType(SpecialType.System_Object)
      : model.GetTypeInfo(node).ConvertedType;
  }

  public static ITypeSymbol GetNullableUnderlyingType(this ITypeSymbol type)
  {
    // If the type is Nullable<T> or T?, convert to T
    if (type is INamedTypeSymbol
        {
          ConstructedFrom.SpecialType: SpecialType.System_Nullable_T,
          Arity: 1
        } nullableMonad)
    {
      return nullableMonad.TypeArguments[0];
    }

    // Pre: Nullable is enabled in the compilation properties
    // In the case of user-defined types, the type will be annotated as nullable
    // instead of being contained by the Nullable monad.
    // We remove the nullable annotation and return the type.
    if (type is INamedTypeSymbol
          {
            NullableAnnotation: NullableAnnotation.Annotated
          }
          nullableAnnotation)
    {
      return nullableAnnotation.WithNullableAnnotation(
        NullableAnnotation.NotAnnotated);
    }

    return type;
  }

  // For reflection use
  private static readonly SymbolDisplayFormat ClrFormatOptions
    = new(
      typeQualificationStyle:
      SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces);

  // Subsumes ToClrTypeName.
  public static string ToFullMetadataName(this ISymbol symbol)
  {
    if (IsRootNamespace(symbol)) return string.Empty;
    var builder = new List<string> { symbol.MetadataName };
    var current = symbol.ContainingSymbol;

    while (!IsRootNamespace(current))
    {
      if (current is ITypeSymbol && symbol is ITypeSymbol)
        builder.Add("+");
      else
        builder.Add(".");

      builder.Add(
        current.OriginalDefinition.ToDisplayString(SymbolDisplayFormat
          .MinimallyQualifiedFormat));
      current = current.ContainingSymbol;
    }

    builder.Reverse();
    return string.Join("", builder);
  }

  private static bool IsRootNamespace(ISymbol symbol)
  {
    return symbol is INamespaceSymbol { IsGlobalNamespace: true };
  }

  public static string ToClrTypeName(this ITypeSymbol type)
  {
    return type.ToDisplayString(ClrFormatOptions);
  }

  public static bool ContainsGenericTypeParameter(this ITypeSymbol typeSymbol)
  {
    return typeSymbol switch
    {
      { TypeKind: TypeKind.TypeParameter } => true,
      INamedTypeSymbol { IsGenericType: true } namedTypeSymbol =>
        namedTypeSymbol.TypeArguments.Any(ContainsGenericTypeParameter),
      _ => false
    };
  }

  /*
   * https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/operators/lambda-expressions
   * A lambda expression can't directly capture an in, ref, or out parameter
   * from the enclosing method.
   * This method determines if the function can capture the value of the ref
   * variable to be considered as operand in the mutated expression.
   */
  public static bool NodeCanBeDelegate(this SemanticModel model,
    SyntaxNode node)
  {
    return node.DescendantNodesAndSelf().OfType<ExpressionSyntax>()
      .All(expr =>
      {
        var symbol = model.GetSymbolInfo(expr).Symbol!;
        return symbol switch
        {
          IMethodSymbol methodSymbol => methodSymbol.Parameters
            .All(param => param.RefKind is RefKind.None),
          IPropertySymbol propertySymbol => propertySymbol.Parameters
            .All(param => param.RefKind is RefKind.None),
          IParameterSymbol paramSymbol => paramSymbol is
            { RefKind: RefKind.None },
          ILocalSymbol localSymbol => localSymbol is { RefKind: RefKind.None },
          _ => true
        };
      });
  }

  public static bool IsSymbolVariable(this ISymbol symbol)
  {
    return symbol switch
    {
      IFieldSymbol fieldSymbol => fieldSymbol is
        { IsConst: false, IsReadOnly: false },
      IPropertySymbol propertySymbol => propertySymbol is
        { IsReadOnly: false, SetMethod: not null },
      IParameterSymbol paramSymbol => paramSymbol.RefKind is RefKind.Ref
        or RefKind.Out,
      ILocalSymbol localSymbol => localSymbol is { IsConst: false },
      _ => false
    };
  }

  public static bool IsTypeSymbolNullable(this ITypeSymbol typeSymbol)
  {
    var underlyingTypeSymbol = typeSymbol.GetNullableUnderlyingType();
    return !SymbolEqualityComparer.IncludeNullability.Equals(typeSymbol,
      underlyingTypeSymbol);
  }

  public static SymbolVisibility GetVisibility(this ISymbol symbol)
  {
    if (symbol is { Kind: SymbolKind.Alias or SymbolKind.TypeParameter })
      return SymbolVisibility.Private;
    if (symbol is { Kind: SymbolKind.Parameter })
      return GetVisibility(symbol.ContainingSymbol);

    var currentSymbol = symbol;
    var visibility = SymbolVisibility.Public;

    while (currentSymbol is { Kind: not SymbolKind.Namespace })
    {
      if (currentSymbol.DeclaredAccessibility is
          Accessibility.Private or Accessibility.NotApplicable)
        return SymbolVisibility.Private;

      if (currentSymbol.DeclaredAccessibility is
          Accessibility.Internal or Accessibility.ProtectedAndInternal)
        visibility = SymbolVisibility.Internal;

      currentSymbol = currentSymbol.ContainingSymbol;
    }

    return visibility;
  }

  public static bool IsNegativeLiteral(this ExpressionSyntax node)
  {
    return node is PrefixUnaryExpressionSyntax unaryExpr &&
           unaryExpr.IsKind(SyntaxKind.UnaryMinusExpression) &&
           unaryExpr.Operand.IsKind(SyntaxKind.NumericLiteralExpression);
  } 
  
  /*
   * Handles positive and negative decimal, hexadecimal, and binary literals.
   */
  public static bool CanImplicitlyConvertNumericLiteral(this SemanticModel model,
    ExpressionSyntax node, SpecialType destinationType)
  {
    if (destinationType is SpecialType.None) return false;
    
    // Determine the type of the numeric literal based on its suffix
    var literalExpression = node switch
    {
      LiteralExpressionSyntax lit when 
        lit.IsKind(SyntaxKind.NumericLiteralExpression) => lit,
      PrefixUnaryExpressionSyntax
          { Operand: LiteralExpressionSyntax absoluteLit } unaryExpr when
        unaryExpr.IsNegativeLiteral() => absoluteLit,
      _ => null
    };
    if (literalExpression is null) return false;
    
    // Get the determined type of the node instead of literal, as node could be
    // a prefix unary followed by literal as operand (signifying negative constant)
    var literalType = model.ResolveTypeSymbol(node)!.SpecialType;
    if (literalType is SpecialType.None) return false;
    
    var literalTypeSymbol = model.Compilation.GetSpecialType(literalType);
    var destinationTypeSymbol = model.Compilation.GetSpecialType(destinationType);
    
    // Only a numeric literal with determined type int can be narrowed implicitly
    // based on its value
    if (literalType is not SpecialType.System_Int32 ||
        destinationType is not 
        (SpecialType.System_SByte or
        SpecialType.System_Byte or
        SpecialType.System_Int16 or
        SpecialType.System_UInt16 or 
        SpecialType.System_UInt32 or
        SpecialType.System_UInt64))
    {
      return
        model.Compilation.HasImplicitConversion(literalTypeSymbol,
          destinationTypeSymbol);
    }

    // Get the literal value as a string
    var literalValueDisplay = node.ToString().Replace("_", string.Empty);

    var isNegative = literalValueDisplay.StartsWith("-");
    if (isNegative)
    {
      literalValueDisplay = literalValueDisplay[1..];
    }
    
    var numberStyle = literalValueDisplay switch
    {
      _ when literalValueDisplay.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
        => NumberStyles.HexNumber,
      _ when literalValueDisplay.StartsWith("0b", StringComparison.OrdinalIgnoreCase)
        => NumberStyles.BinaryNumber,
      _ => NumberStyles.Number
    };

    if (literalValueDisplay.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ||
        literalValueDisplay.StartsWith("0b", StringComparison.OrdinalIgnoreCase))
    {
      literalValueDisplay = literalValueDisplay[2..];
    }

    if (!int.TryParse(literalValueDisplay,
          numberStyle, CultureInfo.InvariantCulture, out var literalValue))
    {
      return false;
    }
    
    // Set the value negative if applicable
    if (isNegative)
    {
      literalValue = -literalValue;
    }

    // If the determined type of an integer literal is int and the value
    // represented by the literal is within the range of the destination type,
    // the value can be implicitly converted to sbyte, byte, short, ushort,
    // uint, ulong, nint or nuint.
    // We evaluate the number and check its range accounting for the negative
    // sign.
    return destinationType switch
    {
      SpecialType.System_SByte => 
        literalValue is >= sbyte.MinValue and <= sbyte.MaxValue,
      SpecialType.System_Byte => 
        literalValue is >= byte.MinValue and <= byte.MaxValue,
      SpecialType.System_Int16 => 
        literalValue is >= short.MinValue and <= short.MaxValue,
      SpecialType.System_UInt16 => 
        literalValue is >= ushort.MinValue and <= ushort.MaxValue,
      SpecialType.System_UInt32 => literalValue >= 0,
      SpecialType.System_UInt64 => literalValue >= 0
    };
  }

  public static (ITypeSymbol returnSymbol, ITypeSymbol leftSymbol, ITypeSymbol
    rightSymbol)?
    ResolveOverloadedPredefinedBinaryOperator(this SemanticModel model,
      FrozenDictionary<SyntaxKind,
          ImmutableArray<MethodSignature>> builtInOperatorSignatures,
      SyntaxKind operatorKind, MethodSignature currentSignature)
  {
    if (!builtInOperatorSignatures.TryGetValue(operatorKind,
          out var candidates))
    {
      return null;
    }

    foreach (var (ret, operands) in candidates)
    {
      var leftCandidateSymbol = operands[0];
      var rightCandidateSymbol = operands[1];

      (ITypeSymbol from, ITypeSymbol to)[] checks =
      [
        (currentSignature.OperandTypes[0], leftCandidateSymbol),
        (currentSignature.OperandTypes[1], rightCandidateSymbol),
        (ret, currentSignature.ReturnType)
      ];

      if (checks.All(type =>
            model.Compilation.HasImplicitConversion(type.from, type.to)))
      {
        return (returnSymbol: ret,
          leftSymbol: leftCandidateSymbol, 
          rightSymbol: rightCandidateSymbol);
      }
    }

    return null;
  }
  
  public static (ITypeSymbol returnSymbol, ITypeSymbol operandSymbol)?
    ResolveOverloadedPredefinedUnaryOperator(this SemanticModel model,
      FrozenDictionary<SyntaxKind,
        ImmutableArray<MethodSignature>> builtInOperatorSignatures,
      SyntaxKind operatorKind, MethodSignature currentSignature)
  {
    if (!builtInOperatorSignatures.TryGetValue(operatorKind,
          out var candidates))
    {
      return null;
    }

    foreach (var (ret, operand) in candidates)
    {
      (ITypeSymbol from, ITypeSymbol to)[] checks =
      [
        (currentSignature.OperandTypes[0], operand[0]),
        (ret, currentSignature.OperandTypes[0])
      ];

      if (checks.All(type =>
            model.Compilation.HasImplicitConversion(type.from, type.to)))
      {
        return (returnSymbol: ret, operandSymbol: operand[0]);
      }
    }

    return null;
  }
  
  /*
   * https://stackoverflow.com/questions/60778208/overload-resolution-with-implicit-conversions
   * https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/language-specification/conversions#102-implicit-conversions
   * https://github.com/dotnet/roslyn/blob/main/docs/specs/CSharp%206/Better%20Betterness.md
   * Given types T1 and T2, T1 is wider if no implicit conversion from T1 to T2
   * exists, and an implicit conversion from T2 to T1 exists.
   * We select the converted type by default as a fallback.
   */
  public static ITypeSymbol DetermineNarrowerNumericType(
    this SemanticModel model,
    ITypeSymbol convertedType, ITypeSymbol exprType)
  {
    var exprToConverted =
      model.Compilation.HasImplicitConversion(exprType, convertedType);

    // Select the converted type if there is no implicit conversion between
    // both types: the literal's exact type will be the converted type
    // Example: there is no implicit conversion between signed and unsigned
    // integral types
    return exprToConverted ? exprType : convertedType;
  }

  public static bool IsAString(this SyntaxNode node)
  {
    return node.IsKind(SyntaxKind.StringLiteralExpression) ||
           node.IsKind(SyntaxKind.Utf8StringLiteralExpression) ||
           node.IsKind(SyntaxKind.InterpolatedStringExpression);
  }

  public static bool IsNumeric(this ITypeSymbol typeSymbol)
  {
    return SupportedType.Numeric.HasFlag(
      GetSpecialTypeClassification(typeSymbol.SpecialType));
  }

  /*
   * https://learn.microsoft.com/en-us/dotnet/api/microsoft.codeanalysis.csharp.syntax.literalexpressionsyntax
   * The following syntax kinds are literal:
     ArgListExpression
     NumericLiteralExpression
     StringLiteralExpression
     Utf8StringLiteralExpression
     CharacterLiteralExpression
     TrueLiteralExpression
     FalseLiteralExpression
     NullLiteralExpression
     DefaultLiteralExpression 
   */
  public static bool IsSyntaxKindLiteral(this SyntaxKind kind)
  {
    return kind is SyntaxKind.ArgListExpression or
      SyntaxKind.NumericLiteralExpression or
      SyntaxKind.StringLiteralExpression or
      SyntaxKind.Utf8StringLiteralExpression or
      SyntaxKind.CharacterLiteralExpression or
      SyntaxKind.TrueLiteralExpression or
      SyntaxKind.FalseLiteralExpression or
      SyntaxKind.NullLiteralExpression or
      SyntaxKind.DefaultLiteralExpression;
  }

  public static bool IsSyntaxKindPrefixOperator(this SyntaxKind kind)
  {
    return kind is SyntaxKind.UnaryPlusExpression or
      SyntaxKind.UnaryMinusExpression or
      SyntaxKind.BitwiseNotExpression or
      SyntaxKind.LogicalNotExpression or
      SyntaxKind.PreIncrementExpression or
      SyntaxKind.PreDecrementExpression;
  }

  /* https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/language-specification/statements#1331-general
   * A block that contains one or more yield statements (§13.15) is called an iterator block.
   * Iterator blocks are used to implement function members as iterators (§15.14).
   * Some additional restrictions apply to iterator blocks:
     It is a compile-time error for a return statement to appear in an iterator block 
     (but yield return statements are permitted).
     It is a compile-time error for an iterator block to contain an unsafe context (§23.2). 
     An iterator block always defines a safe context, even when its declaration is nested 
     in an unsafe context.
   */
  public static bool IsIteratorBlock(BlockSyntax blockSyntax)
  {
    return blockSyntax.DescendantNodes().OfType<YieldStatementSyntax>().Any();
  }

  public static bool IsTypeVoid(this SemanticModel model, ITypeSymbol typeSymbol)
  {
    var voidType = model.Compilation.GetSpecialType(SpecialType.System_Void);
    var taskType =
      model.Compilation.GetTypeByMetadataName("System.Threading.Tasks.Task");

    return typeSymbol.Equals(voidType, SymbolEqualityComparer.Default) ||
           taskType is not null &&
           typeSymbol.Equals(taskType, SymbolEqualityComparer.Default);
  }
}