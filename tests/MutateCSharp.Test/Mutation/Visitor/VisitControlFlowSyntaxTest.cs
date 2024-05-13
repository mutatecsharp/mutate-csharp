using FluentAssertions;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using MutateCSharp.Mutation.Registry;
using Xunit.Abstractions;

namespace MutateCSharp.Test.Mutation.Visitor;

public class VisitControlFlowSyntaxTest(ITestOutputHelper testOutputHelper)
{
  [Theory]
  [InlineData("""
              switch (x)
              {
                case 1:
                  x = 2;
                  break;
              }
              """)]
  [InlineData("""
              switch (x)
              {
                case 1:
                  while (true) { x = 2; }
              }
              """)]
  public void ShouldInsertBreakStatementsAfterEachSwitchSection(string construct)
  {
    var inputUnderMutation =
      $$"""
        using System;

        public class A
        {
          public static void Main()
          {
            var x = 1;
            {{construct}}
          }
        }
        """;
    
    testOutputHelper.WriteLine(inputUnderMutation);
    
    var schemaRegistry = new FileLevelMutantSchemaRegistry();

    var node = TestUtil.GetNodeUnderMutationAfterRewrite
      <SwitchSectionSyntax>(
        inputUnderMutation,
        schemaRegistry,
        (rewriter, node) => rewriter.VisitSwitchSection(node)
      );

    node.Should().BeOfType<SwitchSectionSyntax>();
    var switchSection = (SwitchSectionSyntax)node;
    switchSection.Statements.Last().Should().BeOfType<BreakStatementSyntax>();
  }
  
  [Theory]
  [InlineData("""
              switch (x)
              {
                case 1:
                case 2:
                  x = 2;
                  break;
              }
              """)]
  [InlineData("""
              switch (x)
              {
                case 1:
                case 2:
                  while (true) { x = 2; }
              }
              """)]
  public void ShouldNotInsertBreakStatementsInBetweenCasesInSwitchSections(string construct)
  {
    var inputUnderMutation =
      $$"""
        using System;

        public class A
        {
          public static void Main()
          {
            var x = 1;
            {{construct}}
          }
        }
        """;
    
    testOutputHelper.WriteLine(inputUnderMutation);
    
    var schemaRegistry = new FileLevelMutantSchemaRegistry();

    var mutatedNode = TestUtil.GetNodeUnderMutationAfterRewrite
      <SwitchSectionSyntax>(
        inputUnderMutation,
        schemaRegistry,
        (rewriter, node) => rewriter.VisitSwitchSection(node)
      );

    mutatedNode.Should().BeOfType<SwitchSectionSyntax>();
    var count = mutatedNode.DescendantNodesAndSelf().OfType<SwitchSectionSyntax>().Count();
    count.Should().Be(1);

    var switchSection = (SwitchSectionSyntax)mutatedNode;
    switchSection.Statements.Last().Should().BeOfType<BreakStatementSyntax>();
  }
}