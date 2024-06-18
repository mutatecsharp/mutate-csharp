# mutate-csharp

mutate-csharp is a mutation testing tool for __C#__.

Want to know if your tests can exercise the behaviour
of your code effectively? Based on the idea that tests
that are good at detecting artificial faults are also
effective at detecting real-world bugs,
mutate-csharp injects artificial defects into C# programs 
by systematically _mutating_ the source code of programs 
under test.

To achieve this goal, mutate-csharp supports mutant schemata
generation to encode all behavioural defects into a metaprogram.
mutate-csharp deploys a profiler to identify 
if expressions under mtuation can be reached to determine if it needs
to evaluate the variants of the mutated expression.

mutate-csharp will execute all tests for your project to evaluate
if your tests can detect the artificial fault. Note that this can
take a while since there will be many mutations that can apply!
You can also target individual source files to limit the time taken to
assess your tests.

