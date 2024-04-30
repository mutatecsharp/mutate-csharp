using System.CodeDom;
using System.Collections.Frozen;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CSharp;

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
      new TypeSignature(SupportedType.UnsignedIntegral,
        SupportedType.UnsignedIntegral),
      new TypeSignature(SupportedType.SignedIntegral,
        SupportedType.SignedIntegral),
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

  // For reflection use
  public static readonly FrozenDictionary<string, string> FriendlyNameToClrName
    = BuildDefinedTypeToClrType();
  
  public static readonly FrozenSet<SyntaxKind> ShortCircuitOperators
    = new HashSet<SyntaxKind>
    {
      SyntaxKind.LogicalAndExpression,
      SyntaxKind.LogicalOrExpression,
      SyntaxKind.CoalesceExpression,
      SyntaxKind.ConditionalExpression
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

  private static FrozenDictionary<string, string> BuildDefinedTypeToClrType()
  {
    var mscorlib = Assembly.GetAssembly(typeof(int));
    var definedTypes =
      mscorlib!.DefinedTypes
        .Where(type => type.Namespace?.Equals("System") ?? false);
    using var provider = new CSharpCodeProvider();
    var friendlyToClrName = new Dictionary<string, string>();

    foreach (var type in definedTypes)
    {
      var typeRef = new CodeTypeReference(type);
      var friendlyName = provider.GetTypeOutput(typeRef);
      // Filter qualified types
      if (!friendlyName.Contains('.'))
        friendlyToClrName[friendlyName] = type.FullName!;
    }

    return friendlyToClrName.ToFrozenDictionary();
  }

  public static string ToClrTypeName(this ITypeSymbol type)
  {
    return type.SpecialType != SpecialType.None
      ? FriendlyNameToClrName[type.ToDisplayString()]
      : type.ToDisplayString();
  }

  public static string ToClrTypeName(this Type type)
  {
    using var provider = new CSharpCodeProvider();
    var typeRef = new CodeTypeReference(type);
    return provider.GetTypeOutput(typeRef);
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
    TypeSignature[] TypeSignatures)
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
    TypeSignature[] TypeSignatures)
  {
    public override string ToString()
    {
      return SyntaxFacts.GetText(TokenKind);
    }
  }
}