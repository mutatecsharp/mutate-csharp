using System;
using System.Collections.Generic;
using System.Data;
namespace MutateCSharp
{
    internal class Schemata1
    {
        private static readonly System.Lazy<int> ActivatedMutantId =
          new System.Lazy<int>(() =>
          {
              var activatedMutant = System.Environment.GetEnvironmentVariable("MUTATE_CSHARP_ACTIVATED_MUTANT1");
              return !string.IsNullOrEmpty(activatedMutant) ? int.Parse(activatedMutant) : 0;
          });

        private static bool ActivatedInRange(int lowerBound, int upperBound)
        {
            return lowerBound <= ActivatedMutantId.Value && ActivatedMutantId.Value <= upperBound;
        }
        internal static bool ReplaceBinExprOp_7(int mutantId, int argument1, int argument2)
        {
            if (!ActivatedInRange(mutantId, mutantId + 2)) { return argument1 > argument2; }
            if (ActivatedMutantId.Value == mutantId + 0) { return argument1 != argument2; }
            if (ActivatedMutantId.Value == mutantId + 1) { return argument1 >= argument2; }
            if (ActivatedMutantId.Value == mutantId + 2) { return false; }
            return argument1 > argument2;
        }

        internal static int ReplaceNumericConstant_1(int mutantId, int argument1)
        {
            if (!ActivatedInRange(mutantId, mutantId + 3)) { return argument1; }
            if (ActivatedMutantId.Value == mutantId + 0) { return argument1 + 1; }
            if (ActivatedMutantId.Value == mutantId + 1) { return argument1 - 1; }
            if (ActivatedMutantId.Value == mutantId + 2) { return -argument1; }
            if (ActivatedMutantId.Value == mutantId + 3) { return 0; }
            return argument1;
        }

        internal static int ReplaceBinExprOp_8(int mutantId, int argument1, int argument2)
        {
            if (!ActivatedInRange(mutantId, mutantId + 7)) { return argument1 + argument2; }
            if (ActivatedMutantId.Value == mutantId + 0) { return argument1 - argument2; }
            if (ActivatedMutantId.Value == mutantId + 1) { return argument1 % argument2; }
            if (ActivatedMutantId.Value == mutantId + 2) { return argument1 << argument2; }
            if (ActivatedMutantId.Value == mutantId + 3) { return argument1 >> argument2; }
            if (ActivatedMutantId.Value == mutantId + 4) { return argument1 | argument2; }
            if (ActivatedMutantId.Value == mutantId + 5) { return argument1 & argument2; }
            if (ActivatedMutantId.Value == mutantId + 6) { return argument1 ^ argument2; }
            if (ActivatedMutantId.Value == mutantId + 7) { return argument1; }
            return argument1 + argument2;
        }

        internal static int ReplaceBinExprOp_4(int mutantId, int argument1, int argument2)
        {
            if (!ActivatedInRange(mutantId, mutantId + 10)) { return argument1 - argument2; }
            if (ActivatedMutantId.Value == mutantId + 0) { return argument1 + argument2; }
            if (ActivatedMutantId.Value == mutantId + 1) { return argument1 * argument2; }
            if (ActivatedMutantId.Value == mutantId + 2) { return argument1 / argument2; }
            if (ActivatedMutantId.Value == mutantId + 3) { return argument1 % argument2; }
            if (ActivatedMutantId.Value == mutantId + 4) { return argument1 << argument2; }
            if (ActivatedMutantId.Value == mutantId + 5) { return argument1 >> argument2; }
            if (ActivatedMutantId.Value == mutantId + 6) { return argument1 | argument2; }
            if (ActivatedMutantId.Value == mutantId + 7) { return argument1 & argument2; }
            if (ActivatedMutantId.Value == mutantId + 8) { return argument1 ^ argument2; }
            if (ActivatedMutantId.Value == mutantId + 9) { return argument1; }
            if (ActivatedMutantId.Value == mutantId + 10) { return argument2; }
            return argument1 - argument2;
        }

        internal static bool ReplaceBinExprOp_3(int mutantId, int argument1, int argument2)
        {
            if (!ActivatedInRange(mutantId, mutantId + 2)) { return argument1 <= argument2; }
            if (ActivatedMutantId.Value == mutantId + 0) { return argument1 == argument2; }
            if (ActivatedMutantId.Value == mutantId + 1) { return argument1 < argument2; }
            if (ActivatedMutantId.Value == mutantId + 2) { return true; }
            return argument1 <= argument2;
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

        internal static int ReplaceBinExprOp_2(int mutantId, int argument1, int argument2)
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

        internal static int ReplaceBinExprOp_6(int mutantId, int argument1, int argument2)
        {
            if (!ActivatedInRange(mutantId, mutantId + 10)) { return argument1 + argument2; }
            if (ActivatedMutantId.Value == mutantId + 0) { return argument1 - argument2; }
            if (ActivatedMutantId.Value == mutantId + 1) { return argument1 * argument2; }
            if (ActivatedMutantId.Value == mutantId + 2) { return argument1 / argument2; }
            if (ActivatedMutantId.Value == mutantId + 3) { return argument1 % argument2; }
            if (ActivatedMutantId.Value == mutantId + 4) { return argument1 << argument2; }
            if (ActivatedMutantId.Value == mutantId + 5) { return argument1 >> argument2; }
            if (ActivatedMutantId.Value == mutantId + 6) { return argument1 | argument2; }
            if (ActivatedMutantId.Value == mutantId + 7) { return argument1 & argument2; }
            if (ActivatedMutantId.Value == mutantId + 8) { return argument1 ^ argument2; }
            if (ActivatedMutantId.Value == mutantId + 9) { return argument1; }
            if (ActivatedMutantId.Value == mutantId + 10) { return argument2; }
            return argument1 + argument2;
        }

    }
}

namespace Search
{
    public class BinarySearchIterative<T> : ISearch<T> where T : IComparable<T>
    {
        public int FindIndex(IList<T> collection, T target)
        {
            var l = MutateCSharp.Schemata1.ReplaceNumericConstant_0(1, 0);
            var r = MutateCSharp.Schemata1.ReplaceBinExprOp_2(7, collection.Count, MutateCSharp.Schemata1.ReplaceNumericConstant_1(3, 1));

            while (MutateCSharp.Schemata1.ReplaceBinExprOp_3(15, l, r))
            {
                var mid = MutateCSharp.Schemata1.ReplaceBinExprOp_6(44, l, MutateCSharp.Schemata1.ReplaceBinExprOp_5(33, (MutateCSharp.Schemata1.ReplaceBinExprOp_4(18, r, l)), MutateCSharp.Schemata1.ReplaceNumericConstant_1(29, 2)));
                var current = collection[mid];

                switch (target.CompareTo(current))
                {
                    case > 0:
                        l = mid + 1;
                        break;
                        break;
                    case < 0:
                        r = mid - 1;
                        break;
                        break;
                    default:
                        return mid;
                        break;
                }
            }

            throw new DataException();
            return default;
        }
    }

    public class BinarySearchRecursive<T> : ISearch<T> where T : IComparable<T>
    {
        public int FindIndex(IList<T> collection, T target)
        {
            return FindIndex(collection, target, MutateCSharp.Schemata1.ReplaceNumericConstant_0(55, 0), MutateCSharp.Schemata1.ReplaceBinExprOp_2(61, collection.Count, MutateCSharp.Schemata1.ReplaceNumericConstant_1(57, 1)));
        }

        public static int FindIndex(IList<T> collection, T target, int left, int right)
        {
            if (MutateCSharp.Schemata1.ReplaceBinExprOp_7(69, left, right)) throw new DataException();
            var mid = MutateCSharp.Schemata1.ReplaceBinExprOp_6(98, left, MutateCSharp.Schemata1.ReplaceBinExprOp_5(87, (MutateCSharp.Schemata1.ReplaceBinExprOp_4(72, right, left)), MutateCSharp.Schemata1.ReplaceNumericConstant_1(83, 2)));
            var result = target.CompareTo(collection[mid]);

            return result switch
            {
                > 0 => FindIndex(collection, target, MutateCSharp.Schemata1.ReplaceBinExprOp_8(113, mid, MutateCSharp.Schemata1.ReplaceNumericConstant_1(109, 1)), right),
                < 0 => FindIndex(collection, target, left, MutateCSharp.Schemata1.ReplaceBinExprOp_2(125, mid, MutateCSharp.Schemata1.ReplaceNumericConstant_1(121, 1))),
                _ => mid
            };
        }
    }
}