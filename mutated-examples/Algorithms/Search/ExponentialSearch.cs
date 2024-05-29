using System;
using System.Collections.Generic;
using System.Data;
namespace MutateCSharp
{
    internal class Schemata2
    {
        private static readonly System.Lazy<int> ActivatedMutantId =
          new System.Lazy<int>(() =>
          {
              var activatedMutant = System.Environment.GetEnvironmentVariable("MUTATE_CSHARP_ACTIVATED_MUTANT2");
              return !string.IsNullOrEmpty(activatedMutant) ? int.Parse(activatedMutant) : 0;
          });

        private static bool ActivatedInRange(int lowerBound, int upperBound)
        {
            return lowerBound <= ActivatedMutantId.Value && ActivatedMutantId.Value <= upperBound;
        }
        internal static bool ReplaceBinExprOp_1(int mutantId, int argument1, int argument2)
        {
            if (!ActivatedInRange(mutantId, mutantId + 2)) { return argument1 == argument2; }
            if (ActivatedMutantId.Value == mutantId + 0) { return argument1 <= argument2; }
            if (ActivatedMutantId.Value == mutantId + 1) { return argument1 >= argument2; }
            if (ActivatedMutantId.Value == mutantId + 2) { return false; }
            return argument1 == argument2;
        }

        internal static int ReplaceNumericConstant_2(int mutantId, int argument1)
        {
            if (!ActivatedInRange(mutantId, mutantId + 3)) { return argument1; }
            if (ActivatedMutantId.Value == mutantId + 0) { return argument1 + 1; }
            if (ActivatedMutantId.Value == mutantId + 1) { return argument1 - 1; }
            if (ActivatedMutantId.Value == mutantId + 2) { return -argument1; }
            if (ActivatedMutantId.Value == mutantId + 3) { return 0; }
            return argument1;
        }

        internal static bool ReplaceBinExprOp_3(int mutantId, int argument1, int argument2)
        {
            if (!ActivatedInRange(mutantId, mutantId + 2)) { return argument1 < argument2; }
            if (ActivatedMutantId.Value == mutantId + 0) { return argument1 != argument2; }
            if (ActivatedMutantId.Value == mutantId + 1) { return argument1 <= argument2; }
            if (ActivatedMutantId.Value == mutantId + 2) { return false; }
            return argument1 < argument2;
        }

        internal static bool ReplaceBinExprOp_4(int mutantId, System.Func<bool> argument1, System.Func<bool> argument2)
        {
            if (!ActivatedInRange(mutantId, mutantId + 3)) { return argument1() && argument2(); }
            if (ActivatedMutantId.Value == mutantId + 0) { return argument1() == argument2(); }
            if (ActivatedMutantId.Value == mutantId + 1) { return false; }
            if (ActivatedMutantId.Value == mutantId + 2) { return argument1(); }
            if (ActivatedMutantId.Value == mutantId + 3) { return argument2(); }
            return argument1() && argument2();
        }

        internal static int ReplaceBinExprOp_5(int mutantId, int argument1, int argument2)
        {
            if (!ActivatedInRange(mutantId, mutantId + 10)) { return argument1 / argument2; }
            if (ActivatedMutantId.Value == mutantId + 0) { return argument1 + argument2; }
            if (ActivatedMutantId.Value == mutantId + 1) { return argument1 - argument2; }
            if (ActivatedMutantId.Value == mutantId + 2) { return argument1 * argument2; }
            if (ActivatedMutantId.Value == mutantId + 3) { return argument1 % argument2; }
            if (ActivatedMutantId.Value == mutantId + 4) { return argument1 << argument2; }
            if (ActivatedMutantId.Value == mutantId + 5) { return argument1 >> argument2; }
            if (ActivatedMutantId.Value == mutantId + 6) { return argument1 | argument2; }
            if (ActivatedMutantId.Value == mutantId + 7) { return argument1 & argument2; }
            if (ActivatedMutantId.Value == mutantId + 8) { return argument1 ^ argument2; }
            if (ActivatedMutantId.Value == mutantId + 9) { return argument1; }
            if (ActivatedMutantId.Value == mutantId + 10) { return argument2; }
            return argument1 / argument2;
        }

        internal static int ReplaceBinExprOp_6(int mutantId, int argument1, int argument2)
        {
            if (!ActivatedInRange(mutantId, mutantId + 7)) { return argument1 - argument2; }
            if (ActivatedMutantId.Value == mutantId + 0) { return argument1 + argument2; }
            if (ActivatedMutantId.Value == mutantId + 1) { return argument1 % argument2; }
            if (ActivatedMutantId.Value == mutantId + 2) { return argument1 << argument2; }
            if (ActivatedMutantId.Value == mutantId + 3) { return argument1 >> argument2; }
            if (ActivatedMutantId.Value == mutantId + 4) { return argument1 | argument2; }
            if (ActivatedMutantId.Value == mutantId + 5) { return argument1 & argument2; }
            if (ActivatedMutantId.Value == mutantId + 6) { return argument1 ^ argument2; }
            if (ActivatedMutantId.Value == mutantId + 7) { return argument1; }
            return argument1 - argument2;
        }

        internal static int ReplaceNumericConstant_0(int mutantId, int argument1)
        {
            if (!ActivatedInRange(mutantId, mutantId + 1)) { return argument1; }
            if (ActivatedMutantId.Value == mutantId + 0) { return argument1 + 1; }
            if (ActivatedMutantId.Value == mutantId + 1) { return argument1 - 1; }
            return argument1;
        }

    }
}

namespace Search
{
    public class ExponentialSearch<T> : ISearch<T> where T : IComparable<T>
    {
        public int FindIndex(IList<T> collection, T target)
        {
            if (MutateCSharp.Schemata2.ReplaceBinExprOp_1(3, collection.Count, MutateCSharp.Schemata2.ReplaceNumericConstant_0(1, 0))) throw new DataException();

            var bound = MutateCSharp.Schemata2.ReplaceNumericConstant_2(6, 1);
            while (MutateCSharp.Schemata2.ReplaceBinExprOp_4(18, () => MutateCSharp.Schemata2.ReplaceBinExprOp_3(10, bound, collection.Count), () => MutateCSharp.Schemata2.ReplaceBinExprOp_3(15, collection[bound].CompareTo(target), MutateCSharp.Schemata2.ReplaceNumericConstant_0(13, 0))))
            {
                bound *= MutateCSharp.Schemata2.ReplaceNumericConstant_2(22, 2);
            }

            return BinarySearchRecursive<T>.FindIndex(collection, target, MutateCSharp.Schemata2.ReplaceBinExprOp_5(30, bound, MutateCSharp.Schemata2.ReplaceNumericConstant_2(26, 2)),
              Math.Min(bound, MutateCSharp.Schemata2.ReplaceBinExprOp_6(45, collection.Count, MutateCSharp.Schemata2.ReplaceNumericConstant_2(41, 1))));
        }
    }
}