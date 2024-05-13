using System.CodeDom;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CSharp;
using Serilog;

namespace MutateCSharp.Util;

public static class ReflectionUtil
{
  public static string ToClrTypeName(this Type type)
  {
    using var provider = new CSharpCodeProvider();
    var typeRef = new CodeTypeReference(type);
    return provider.GetTypeOutput(typeRef);
  }

  public static Type ConstructNullableValueType(
    this Type valueType, Assembly sutAssembly)
  {
    var nullableType = ResolveReflectionType("System.Nullable`1", sutAssembly)!;
    return nullableType.MakeGenericType(valueType);
  }
  
  public static Type? ResolveReflectionType(string typeName,
    Assembly sutAssembly)
  {
    // If we cannot locate the type from the assembly of SUT, this means we
    // are looking for types defined in the core library: we defer to the
    // current assembly to get the type's runtime type
    return sutAssembly.GetType(typeName) ?? Type.GetType(typeName);
  }
  
  public static Type? GetRuntimeType(this ITypeSymbol typeSymbol,
    Assembly sutAssembly)
  {
    // Named type (user-defined, predefined collection types, etc.)
    if (typeSymbol is INamedTypeSymbol namedTypeSymbol)
    {
      // Construct type
      var runtimeBaseType =
        ResolveReflectionType(namedTypeSymbol.ToFullMetadataName(),
          sutAssembly);

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
      var elementType =
        GetRuntimeType(arrayTypeSymbol.ElementType, sutAssembly);
      return arrayTypeSymbol.Rank == 1
        ? elementType?.MakeArrayType()
        : elementType?.MakeArrayType(arrayTypeSymbol.Rank);
    }

    // Pointer type
    if (typeSymbol is IPointerTypeSymbol pointerTypeSymbol)
    {
      var elementType =
        GetRuntimeType(pointerTypeSymbol.PointedAtType, sutAssembly);
      return elementType?.MakePointerType();
    }

    // Unsupported type
    Log.Debug(
      "Cannot obtain runtime type from assembly due to unsupported type symbol: {TypeName}. Ignoring...",
      typeSymbol.GetType().FullName);
    return null;
  }

  
}