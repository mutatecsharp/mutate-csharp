using System;
using System.Collections.Generic;
using System.Data;
namespace MutateCSharp
{
    internal class Schemata4
    {
        private static readonly System.Lazy<string> MutantTracerFilePath =
          new System.Lazy<string>(() =>
          {
              var tracerFilePath = System.Environment.GetEnvironmentVariable("MUTATE_CSHARP_TRACER_FILEPATH");
              return !string.IsNullOrEmpty(tracerFilePath) ? tracerFilePath : string.Empty;
          });

        private static readonly System.Collections.Concurrent
          .ConcurrentDictionary<int, byte> MutantsExecuted
            = new(System.Environment.ProcessorCount, capacity: 12);

        private static bool MutantIsAlreadyTraced(int lowerBound)
        {
            return string.IsNullOrEmpty(MutantTracerFilePath.Value) ||
              MutantsExecuted.ContainsKey(lowerBound);
        }

        private static void RecordMutantExecution(int lowerBound, int mutationCount)
        {
            var executedMutants = new System.Collections.Generic.List<string>();

            for (var i = lowerBound; i < lowerBound + mutationCount; i++)
            {
                MutantsExecuted.TryAdd(i, byte.MinValue);
                executedMutants.Add($"MUTATE_CSHARP_ACTIVATED_MUTANT4:{i}{System.Environment.NewLine}");
            }

            // Persist mutant execution trace to disk
            System.IO.File.AppendAllText(MutantTracerFilePath.Value, string.Join(string.Empty, executedMutants));
        }
        internal static int TraceReplaceNumericConstant_0(int baseMutantId, int argument1)
        {
            if (MutantIsAlreadyTraced(baseMutantId)) { return argument1; }
            RecordMutantExecution(baseMutantId, 2);
            return argument1;
        }

        internal static bool TraceReplaceBinExprOp_1(int baseMutantId, int argument1, int argument2)
        {
            if (MutantIsAlreadyTraced(baseMutantId)) { return argument1 < argument2; }
            RecordMutantExecution(baseMutantId, 3);
            return argument1 < argument2;
        }

        internal static int TraceReplacePostfixUnaryExprOp_2(int baseMutantId, ref int argument1)
        {
            if (MutantIsAlreadyTraced(baseMutantId)) { return argument1++; }
            RecordMutantExecution(baseMutantId, 2);
            return argument1++;
        }

        internal static bool TraceReplaceBinExprOp_3(int baseMutantId, int argument1, int argument2)
        {
            if (MutantIsAlreadyTraced(baseMutantId)) { return argument1 == argument2; }
            RecordMutantExecution(baseMutantId, 3);
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
            for (var i = MutateCSharp.Schemata4.TraceReplaceNumericConstant_0(1, 0); MutateCSharp.Schemata4.TraceReplaceBinExprOp_1(3, i, collection.Count); MutateCSharp.Schemata4.TraceReplacePostfixUnaryExprOp_2(6, ref i))
            {
                if (MutateCSharp.Schemata4.TraceReplaceBinExprOp_3(10, collection[i].CompareTo(target), MutateCSharp.Schemata4.TraceReplaceNumericConstant_0(8, 0))) return i;
            }

            throw new DataException();
            return default;
        }
    }
}