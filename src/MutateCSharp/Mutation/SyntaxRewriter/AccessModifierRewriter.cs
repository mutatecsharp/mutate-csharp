using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using MutateCSharp.Util;

namespace MutateCSharp.Mutation.SyntaxRewriter;

/*
 * Syntactic modification of access modifier.
 * Widen access modifiers of declaration syntaxes - this allows the mutant
 * schemata to access private or protected types.
 */
public class AccessModifierRewriter : CSharpSyntaxRewriter
{
 /* https://learn.microsoft.com/en-us/dotnet/csharp/programming-guide/classes-and-structs/access-modifiers#class-and-struct-accessibility
  * Classes and structs declared directly within a namespace
  * (aren't nested within other classes or structs) can be either public or
  * internal. internal is the default if no access modifier is specified.
  */
 public override SyntaxNode VisitClassDeclaration(ClassDeclarationSyntax node)
 {
  var nodeWithMutatedChildren =
   (ClassDeclarationSyntax)base.VisitClassDeclaration(node)!;

  // Modify access declaration
  // Set minimum accessibility to public
  var modifier =
   SyntaxRewriterUtil.SetMinimumAccessibilityToPublic(node.Modifiers);

  return nodeWithMutatedChildren.WithModifiers(modifier);
 }

 public override SyntaxNode VisitStructDeclaration(StructDeclarationSyntax node)
 {
  var nodeWithMutatedChildren =
   (StructDeclarationSyntax)base.VisitStructDeclaration(node)!;

  // Modify access declaration
  // Set minimum accessibility to public
  var modifier =
   SyntaxRewriterUtil.SetMinimumAccessibilityToPublic(node.Modifiers);

  return nodeWithMutatedChildren.WithModifiers(modifier);
 }

 /*
  * Interfaces declared directly within a namespace can be public or internal
  * and, just like classes and structs, interfaces default to internal access.
  */
 public override SyntaxNode VisitInterfaceDeclaration(
  InterfaceDeclarationSyntax node)
 {
  var nodeWithMutatedChildren =
   (InterfaceDeclarationSyntax)base.VisitInterfaceDeclaration(node)!;

  // Modify access declaration
  // Set minimum accessibility to internal
  var modifier =
   SyntaxRewriterUtil.SetMinimumAccessibilityToPublic(node.Modifiers);

  return nodeWithMutatedChildren.WithModifiers(modifier);
 }
}
