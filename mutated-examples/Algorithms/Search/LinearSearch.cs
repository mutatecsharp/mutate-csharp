using System;
using System.Collections.Generic;
using System.Data;
namespace MutateCSharp
{
    internal class Schemata4
    {
        private static readonly System.Lazy<int> ActivatedMutantId =
          new System.Lazy<int>(() =>
          {
              var activatedMutant = System.Environment.GetEnvironmentVariable("MUTATE_CSHARP_ACTIVATED_MUTANT4");
              return !string.IsNullOrEmpty(activatedMutant) ? int.Parse(activatedMutant) : 0;
          });

        private static bool ActivatedInRange(int lowerBound, int upperBound)
        {
            return lowerBound <= ActivatedMutantId.Value && ActivatedMutantId.Value <= upperBound;
        }
        internal static int ReplaceNumericConstant_0(int mutantId, int argument1)
        {
            if (!ActivatedInRange(mutantId, mutantId + 1)) { return argument1; }
            if (ActivatedMutantId.Value == mutantId + 0) { return argument1 + 1; }
            if (ActivatedMutantId.Value == mutantId + 1) { return argument1 - 1; }
            return argument1;
        }

        internal static bool ReplaceBinExprOp_1(int mutantId, int argument1, int argument2)
        {
            if (!ActivatedInRange(mutantId, mutantId + 2)) { return argument1 < argument2; }
            if (ActivatedMutantId.Value == mutantId + 0) { return argument1 != argument2; }
            if (ActivatedMutantId.Value == mutantId + 1) { return argument1 <= argument2; }
            if (ActivatedMutantId.Value == mutantId + 2) { return false; }
            return argument1 < argument2;
        }

        internal static int ReplacePostfixUnaryExprOp_2(int mutantId, ref int argument1)
        {
            if (!ActivatedInRange(mutantId, mutantId + 1)) { return argument1++; }
            if (ActivatedMutantId.Value == mutantId + 0) { return argument1--; }
            if (ActivatedMutantId.Value == mutantId + 1) { return argument1; }
            return argument1++;
        }

        internal static bool ReplaceBinExprOp_3(int mutantId, int argument1, int argument2)
        {
            if (!ActivatedInRange(mutantId, mutantId + 2)) { return argument1 == argument2; }
            if (ActivatedMutantId.Value == mutantId + 0) { return argument1 <= argument2; }
            if (ActivatedMutantId.Value == mutantId + 1) { return argument1 >= argument2; }
            if (ActivatedMutantId.Value == mutantId + 2) { return false; }
            return argument1 == argument2;
        }

    }
}

namespace Search
{
    public class LinearSearch<T> : ISearch<T> where T : IComparable<T>
    {
        public int FindIndex(IList<T> collection, T target)
        {
            for (var i = MutateCSharp.Schemata4.ReplaceNumericConstant_0(1, 0); MutateCSharp.Schemata4.ReplaceBinExprOp_1(3, i, collection.Count); MutateCSharp.Schemata4.ReplacePostfixUnaryExprOp_2(6, ref i))
            {
                if (MutateCSharp.Schemata4.ReplaceBinExprOp_3(10, collection[i].CompareTo(target), MutateCSharp.Schemata4.ReplaceNumericConstant_0(8, 0))) return i;
            }

            throw new DataException();
            return default;
        }
    }
}