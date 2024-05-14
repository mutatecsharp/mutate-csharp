using System.Collections.Frozen;
using System.Collections.Immutable;
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

  private static FrozenDictionary<SyntaxKind,
      ImmutableArray<(SpecialType returnType,
        SpecialType leftOperandType,
        SpecialType rightOperandType)>>
    BuildBinaryNumericOperatorMethodSignature()
  {
    var dictionary = new Dictionary<SyntaxKind, ImmutableArray<
      (SpecialType returnType,
      SpecialType leftOperandType,
      SpecialType rightOperandType)>>();

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
      dictionary[operatorKind] =
        [..arithmeticTypes.Select(type => (type, type, type))];
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
      dictionary[operatorKind] =
      [
        ..smallerThanWordIntegralTypes.Concat(arithmeticTypes)
          .Select(type => (type, type, type))
      ];
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
      dictionary[operatorKind] =
      [
        ..arithmeticTypes.Select(type =>
          (returnType: SpecialType.System_Boolean, type, type))
      ];
    }

    // Equality
    foreach (var operatorKind in new[]
             {
               SyntaxKind.EqualsExpression,
               SyntaxKind.NotEqualsExpression
             })
    {
      dictionary[operatorKind] =
      [
        ..allSupportedTypes.Select(type =>
          (returnType: SpecialType.System_Boolean, type, type))
      ];
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
      dictionary[operatorKind] =
      [
        ..integralTypes.Select(type =>
          (type, type, rightOperandType: SpecialType.System_Int32))
      ];
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
      dictionary[operatorKind] =
      [
        ..smallerThanWordIntegralTypes.Concat(integralTypes).Select(type =>
          (type, type, rightOperandType: SpecialType.System_Int32))
      ];
    }

    // (Bitwise / Boolean) Logical
    foreach (var operatorKind in new[]
             {
               SyntaxKind.BitwiseAndExpression,
               SyntaxKind.BitwiseOrExpression,
               SyntaxKind.ExclusiveOrExpression
             })
    {
      dictionary[operatorKind] =
      [
        ..integralTypes.Concat([SpecialType.System_Boolean])
          .Select(type => (type, type, type))
      ];
    }

    // Compound (bitwise / boolean) logical
    foreach (var operatorKind in new[]
             {
               SyntaxKind.AndAssignmentExpression,
               SyntaxKind.OrAssignmentExpression,
               SyntaxKind.ExclusiveOrAssignmentExpression
             })
    {
      dictionary[operatorKind] =
      [
        ..smallerThanWordIntegralTypes.Concat(integralTypes)
          .Concat([SpecialType.System_Boolean])
          .Select(type => (type, type, type))
      ];
    }

    // Conditional logical
    foreach (var operatorKind in new[]
             {
               SyntaxKind.LogicalAndExpression, 
               SyntaxKind.LogicalOrExpression
             })
    {
      dictionary[operatorKind] =
      [
        (SpecialType.System_Boolean, SpecialType.System_Boolean,
          SpecialType.System_Boolean)
      ];
    }

    return dictionary.ToFrozenDictionary();
  }
  
  private static FrozenDictionary<SyntaxKind,
      ImmutableArray<(SpecialType returnType,
        SpecialType operandType)>>
    BuildUnaryNumericOperatorMethodSignature()
  {
    var dictionary = new Dictionary<SyntaxKind, ImmutableArray<
      (SpecialType returnType,
      SpecialType operandType)>>();

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
    
    // Arithmetic
    dictionary[SyntaxKind.UnaryPlusExpression]
      = [..arithmeticTypes.Select(type => (type, type))];
    dictionary[SyntaxKind.UnaryMinusExpression]
      = [
        ..new[] { SpecialType.System_Int32, SpecialType.System_Int64 }
          .Concat(floatingPointTypes)
          .Select(type => (type, type))
      ];
    
    // Logical
    dictionary[SyntaxKind.LogicalNotExpression]
      = [(SpecialType.System_Boolean, SpecialType.System_Boolean)];
    
    // Bitwise complement
    dictionary[SyntaxKind.BitwiseNotExpression]
      = [..integralTypes.Select(type => (type, type))]; 

    // Increment/decrement
    foreach (var operatorKind in new[]
             {
               SyntaxKind.PreIncrementExpression,
               SyntaxKind.PostIncrementExpression,
               SyntaxKind.PreDecrementExpression,
               SyntaxKind.PostDecrementExpression
             })
    {
      dictionary[operatorKind] =
      [..smallerThanWordIntegralTypes.Concat(arithmeticTypes).
        Select(type => (type, type))];
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
  
  public static SpecialType DetermineNumericLiteralType(
    LiteralExpressionSyntax literalExpression)
  {
    if (!literalExpression.IsKind(SyntaxKind.NumericLiteralExpression))
      return SpecialType.None;

    // Get the literal token
    var literalToken = literalExpression.Token;
    var literalValue = literalToken.ValueText;

    // Get the literal suffix (if any)
    // Determine the type of the literal based on its suffix
    var literalSuffix = literalToken.Text[literalValue.Length..];

    // If the literal has no suffix, its type is the first of the following
    // types in which its value can be represented: int, uint, long, ulong.
    if (string.IsNullOrEmpty(literalSuffix))
    {
      return literalValue switch
      {
        _ when int.TryParse(literalValue, out _) => SpecialType.System_Int32,
        _ when uint.TryParse(literalValue, out _) => SpecialType.System_UInt32,
        _ when long.TryParse(literalValue, out _) => SpecialType.System_Int64,
        _ when ulong.TryParse(literalValue, out _) => SpecialType.System_UInt64,
        _ => SpecialType.None
      };
    }

    // If the literal is suffixed by L or l, its type is the first of the
    // following types in which its value can be represented: long, ulong.
    if (literalSuffix.Equals("l", StringComparison.OrdinalIgnoreCase))
    {
      return literalValue switch
      {
        _ when long.TryParse(literalValue, out _) => SpecialType.System_Int64,
        _ when ulong.TryParse(literalValue, out _) => SpecialType.System_UInt64,
        _ => SpecialType.None
      };
    }

    // If the literal is suffixed by U or u, its type is the first of the
    // following types in which its value can be represented: uint, ulong.
    if (literalSuffix.Equals("u", StringComparison.OrdinalIgnoreCase))
    {
      return literalValue switch
      {
        _ when uint.TryParse(literalValue, out _) => SpecialType.System_UInt32,
        _ when ulong.TryParse(literalValue, out _) => SpecialType.System_UInt64,
        _ => SpecialType.None
      };
    }

    // If the literal is suffixed by UL, Ul, uL, ul, LU, Lu, lU, or lu,
    // its type is ulong.
    if (literalSuffix.Equals("ul", StringComparison.OrdinalIgnoreCase) ||
        literalSuffix.Equals("lu", StringComparison.OrdinalIgnoreCase))
    {
      return ulong.TryParse(literalValue, out _)
        ? SpecialType.System_UInt64
        : SpecialType.None;
    }
    
    // If the literal is suffixed by F or f, its type is float.
    if (literalSuffix.Equals("f", StringComparison.OrdinalIgnoreCase))
    {
      return float.TryParse(literalValue, out _)
        ? SpecialType.System_Single
        : SpecialType.None;
    }

    // If the literal is suffixed by D or d, its type is double.
    if (literalSuffix.Equals("d", StringComparison.OrdinalIgnoreCase))
    {
      return double.TryParse(literalValue, out _)
        ? SpecialType.System_Double
        : SpecialType.None;
    }

    // If the literal is suffixed by M or m, its type is decimal.
    if (literalSuffix.Equals("m", StringComparison.OrdinalIgnoreCase))
    {
      return decimal.TryParse(literalValue, out _)
        ? SpecialType.System_Decimal
        : SpecialType.None;
    }

    // Unsupported or invalid literal suffix
    return SpecialType.None;
  }

  public static bool CanImplicitlyConvertNumericLiteral(this SemanticModel model,
    SyntaxNode node, SpecialType destinationType)
  {
    if (!node.IsKind(SyntaxKind.NumericLiteralExpression) 
        || destinationType is SpecialType.None) return false;
    
    // Determine the type of the numeric literal based on its suffix
    var literalExpression = (LiteralExpressionSyntax)node;
    var literalType = DetermineNumericLiteralType(literalExpression);
    if (literalType is SpecialType.None) return false;
    
    var literalTypeSymbol = model.Compilation.GetSpecialType(literalType);
    var destinationTypeSymbol = model.Compilation.GetSpecialType(destinationType);
    
    // Only a numeric literal with determined type int can be narrowed implicitly
    // based on its value
    if (destinationType is not 
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
    var literalValue = literalExpression.Token.ValueText;

    // If the determined type of an integer literal is int and the value
    // represented by the literal is within the range of the destination type,
    // the value can be implicitly converted to sbyte, byte, short, ushort,
    // uint, ulong, nint or nuint:
    return destinationType switch
    {
      SpecialType.System_SByte => sbyte.TryParse(literalValue, out _),
      SpecialType.System_Byte => byte.TryParse(literalValue, out _),
      SpecialType.System_Int16 => short.TryParse(literalValue, out _),
      SpecialType.System_UInt16 => ushort.TryParse(literalValue, out _),
      SpecialType.System_UInt32 => uint.TryParse(literalValue, out _),
      SpecialType.System_UInt64 => ulong.TryParse(literalValue, out _),
      _ => false
    };
  }

  public static (ITypeSymbol returnSymbol, ITypeSymbol leftSymbol, ITypeSymbol
    rightSymbol)?
    ResolveOverloadedPredefinedBinaryOperator(this SemanticModel model,
      SyntaxKind operatorKind, SpecialType returnType, SpecialType leftType,
      SpecialType rightType)
  {
    if (returnType is SpecialType.None || leftType is SpecialType.None ||
        rightType is SpecialType.None) return null;
    
    var candidates =
      PredefinedBinaryOperatorMethodSignatures[operatorKind];
      
    // Get type symbol
    var leftSymbol = model.Compilation.GetSpecialType(leftType);
    var rightSymbol = model.Compilation.GetSpecialType(rightType);
    var retSymbol = model.Compilation.GetSpecialType(returnType);

    foreach (var (ret, left, right) in candidates)
    {
      var retCandidateSymbol = model.Compilation.GetSpecialType(ret);
      var leftCandidateSymbol = model.Compilation.GetSpecialType(left);
      var rightCandidateSymbol = model.Compilation.GetSpecialType(right);

      (ITypeSymbol from, ITypeSymbol to)[] checks =
      [
        (leftSymbol, leftCandidateSymbol),
        (rightSymbol, rightCandidateSymbol),
        (retCandidateSymbol, retSymbol)
      ];

      if (checks.All(type =>
            model.Compilation.HasImplicitConversion(type.from, type.to)))
      {
        return (returnSymbol: retCandidateSymbol,
          leftSymbol: leftCandidateSymbol, 
          rightSymbol: rightCandidateSymbol);
      }
    }

    return null;
  }
  
  public static (ITypeSymbol returnSymbol, ITypeSymbol operandSymbol)?
    ResolveOverloadedPredefinedUnaryOperator(this SemanticModel model,
      SyntaxKind operatorKind, SpecialType returnType, SpecialType operandType)
  {
    var candidates =
      PredefinedUnaryOperatorMethodSignatures[operatorKind];
      
    // Get type symbol
    var operandSymbol = model.Compilation.GetSpecialType(operandType);
    var retSymbol = model.Compilation.GetSpecialType(returnType);

    foreach (var (ret, operand) in candidates)
    {
      var retCandidateSymbol = model.Compilation.GetSpecialType(ret);
      var operandCandidateSymbol = model.Compilation.GetSpecialType(operand);

      (ITypeSymbol from, ITypeSymbol to)[] checks =
      [
        (operandSymbol, operandCandidateSymbol),
        (retCandidateSymbol, retSymbol)
      ];

      if (checks.All(type =>
            model.Compilation.HasImplicitConversion(type.from, type.to)))
      {
        return (returnSymbol: retCandidateSymbol,
          operandSymbol: operandCandidateSymbol);
      }
    }

    return null;
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

  public static bool IsEnumerable(this SemanticModel model,
    ITypeSymbol typeSymbol)
  {
    SpecialType[] enumerableInterfaces =
    [
      SpecialType.System_Collections_Generic_IEnumerable_T,
      SpecialType.System_Collections_IEnumerable
    ];

    return enumerableInterfaces.Any(type =>
      typeSymbol.AllInterfaces.Contains(
        model.Compilation.GetSpecialType(type)));
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
  
  public static readonly
    FrozenDictionary<SyntaxKind, ImmutableArray<(SpecialType returnType,
      SpecialType leftOperandType, SpecialType rightOperandType)>>
    PredefinedBinaryOperatorMethodSignatures = BuildBinaryNumericOperatorMethodSignature();

  private static readonly FrozenDictionary<SyntaxKind,
    ImmutableArray<(SpecialType returnType,
      SpecialType operandType)>> PredefinedUnaryOperatorMethodSignatures
    = BuildUnaryNumericOperatorMethodSignature();
}