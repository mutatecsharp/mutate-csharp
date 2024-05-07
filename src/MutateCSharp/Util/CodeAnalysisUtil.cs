using System.CodeDom;
using System.Collections.Frozen;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CSharp;
using Serilog;

namespace MutateCSharp.Util;

public static class CodeAnalysisUtil
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

  public static readonly TypeSignature[] IncrementOrDecrementTypeSignature
    =
    [
      new TypeSignature(SupportedType.Integral,
        SupportedType.Integral),
      new TypeSignature(SupportedType.FloatingPoint,
        SupportedType.FloatingPoint),
      new TypeSignature(SupportedType.Character, SupportedType.Character)
    ];

  // char arithmetic operations return int type
  public static readonly TypeSignature[] ArithmeticTypeSignature
    =
    [
      new TypeSignature(SupportedType.Numeric | SupportedType.Character,
        SupportedType.Numeric)
    ];

  // char bitwise logical operations return int type
  public static readonly TypeSignature[] BitwiseLogicalTypeSignature
    =
    [
      new TypeSignature(SupportedType.Integral | SupportedType.Character,
        SupportedType.Integral),
      new TypeSignature(SupportedType.Boolean, SupportedType.Boolean)
    ];

  public static readonly TypeSignature[] BitwiseShiftTypeSignature
    = [new TypeSignature(SupportedType.Integral, SupportedType.Integral)];

  public static readonly TypeSignature[] BooleanLogicalTypeSignature
    = [new TypeSignature(SupportedType.Boolean, SupportedType.Boolean)];

  public static readonly TypeSignature[] EqualityTypeSignature
    = [new TypeSignature(SupportedType.All, SupportedType.Boolean)];

  public static readonly TypeSignature[] InequalityTypeSignature
    =
    [
      new TypeSignature(SupportedType.Numeric | SupportedType.Character,
        SupportedType.Boolean)
    ];
  
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
  
  public static ITypeSymbol? ResolveType(this Microsoft.CodeAnalysis.TypeInfo typeInfo)
  {
    // Get the type; failing which we get the converted type
    // This mainly happens when we try to get the type of null, but null does
    // not have a type; it will be converted to the object type
    return typeInfo.Type ?? typeInfo.ConvertedType;
  }

  public static Type? ResolveReflectionType(string typeName, Assembly sutAssembly)
  {
    // If we cannot locate the type from the assembly of SUT, this means we
    // are looking for types defined in the core library: we defer to the
    // current assembly to get the type's runtime type
    return sutAssembly.GetType(typeName) ?? Type.GetType(typeName);
  }

  public static ITypeSymbol? GetNullableUnderlyingType(this ITypeSymbol? type)
  {
    // If the type is Nullable<T> or T?, convert to T
    if (type is INamedTypeSymbol 
          { ConstructedFrom.SpecialType: SpecialType.System_Nullable_T,
            Arity: 1 } nullableMonad)
    {
      return nullableMonad.TypeArguments[0];
    }
    
    // Pre: Nullable is enabled in the compilation properties
    // In the case of user-defined types, the type will be annotated as nullable
    // instead of being contained by the Nullable monad.
    // We remove the nullable annotation and return the type.
    if (type is INamedTypeSymbol { NullableAnnotation: NullableAnnotation.Annotated }
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
    var builder = new List<string>{symbol.MetadataName};
    var current = symbol.ContainingSymbol;

    while (!IsRootNamespace(current))
    {
      if (current is ITypeSymbol && symbol is ITypeSymbol)
        builder.Add("+");
      else
        builder.Add(".");
      
      builder.Add(current.OriginalDefinition.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat));
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

  public static string ToClrTypeName(this Type type)
  {
    using var provider = new CSharpCodeProvider();
    var typeRef = new CodeTypeReference(type);
    return provider.GetTypeOutput(typeRef);
  }
  
  public static Type? GetRuntimeType(this ITypeSymbol typeSymbol, Assembly sutAssembly)
  {
    // Named type (user-defined, predefined collection types, etc.)
    if (typeSymbol is INamedTypeSymbol namedTypeSymbol)
    {
      // Construct type
      var runtimeBaseType =
        ResolveReflectionType(namedTypeSymbol.ToFullMetadataName(), sutAssembly);
      
      // Non-generic
      if (!namedTypeSymbol.IsGenericType) return runtimeBaseType;
      
      // Generic (recursively construct children runtime types)
      var runtimeChildrenTypes = namedTypeSymbol.TypeArguments
        .Select(type => GetRuntimeType(type, sutAssembly)).ToList();
      if (runtimeChildrenTypes.Any(type => type is null)) return null;
      var absoluteRuntimeChildrenTypes 
        = runtimeChildrenTypes.Select(type => type!).ToArray();
      return runtimeBaseType?.MakeGenericType(absoluteRuntimeChildrenTypes);
    }
    
    // Array type
    if (typeSymbol is IArrayTypeSymbol arrayTypeSymbol)
    {
      var elementType = GetRuntimeType(arrayTypeSymbol.ElementType, sutAssembly);
      return arrayTypeSymbol.Rank == 1 
        ? elementType?.MakeArrayType() 
        : elementType?.MakeArrayType(arrayTypeSymbol.Rank);
    }
    
    // Pointer type
    if (typeSymbol is IPointerTypeSymbol pointerTypeSymbol)
    {
      var elementType = GetRuntimeType(pointerTypeSymbol.PointedAtType, sutAssembly);
      return elementType?.MakePointerType();
    }
    
    // Unsupported type
    Log.Debug("Cannot obtain runtime type from assembly due to unsupported type symbol: {TypeName}. Ignoring...", 
      typeSymbol.GetType().FullName);
    return null;
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

  public static bool IsAString(this SyntaxNode node)
  {
    return node.IsKind(SyntaxKind.StringLiteralExpression) ||
           node.IsKind(SyntaxKind.Utf8StringLiteralExpression) ||
           node.IsKind(SyntaxKind.InterpolatedStringExpression);
  }

  public record TypeSignature(
    SupportedType OperandType,
    SupportedType ReturnType);

  public record UnaryOp(
    SyntaxKind ExprKind,
    SyntaxKind TokenKind,
    string MemberName,
    TypeSignature[] TypeSignatures,
    Func<SpecialType, bool> PrimitiveTypesToExclude)
  {
    public override string ToString()
    {
      return SyntaxFacts.GetText(TokenKind);
    }
  }

  public record BinOp(
    SyntaxKind ExprKind,
    SyntaxKind TokenKind,
    string MemberName,
    TypeSignature[] TypeSignatures,
    Func<SpecialType, bool> PrimitiveTypesToExclude)
  {
    public override string ToString()
    {
      return SyntaxFacts.GetText(TokenKind);
    }
  }

  public static readonly Func<SpecialType, bool> NothingToExclude = _ => false;
}