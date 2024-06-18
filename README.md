# mutate-csharp: a semantic-aware C# syntax mutator

`mutate-csharp` is a mutation testing tool for __C#__.

Want to know if your tests can exercise the behaviour
of your code effectively?

Based on the idea that tests
that are effective at detecting artificial faults are also
effective at detecting real-world bugs,
`mutate-csharp` injects artificial defects into C# programs 
by systematically _mutating_ the source code of programs 
under test.

## Features

- Mutant generation: _mutants_ are program variants that are
equvalent to the original program except for the
mutated expression. `mutate-csharp` supports mutant schemata
generation to encode all behavioural defects into a metaprogram.

- Mutation analysis: `mutate-csharp` will execute all tests for your project to evaluate
if your tests can detect the artificial fault. Note that this can
take a while since there will be many mutations that can apply!

- Mutant profiling: Using the mutant coverage information,
`mutate-csharp` can speed up analysis time by only running mutants
reachable by each of the tests.

## Getting started

### Cloning repository

To clone `mutate-csharp`, run:
```sh
git clone --recursive git@github.com:mutatecsharp/mutate-csharp.git
```

### Dependencies

Building `mutate-csharp` from source requires .NET Runtime 8.0. More information
on installing .NET Runtime 8.0 can be found on 
[.NET's website](https://dotnet.microsoft.com/en-us/download/dotnet/8.0).

### Build instructions

The recommended way to run `mutate-csharp` is to build the `mutate-csharp`
source code with `dotnet`:

```sh
dotnet restore MutateCSharp.sln
dotnet build --no-restore -c Release MutateCSharp.sln
```

The build produces an executable which can be located in the artifact folder and
run:
```sh
dotnet artifacts/MutateCSharp/bin/Release/net8.0/MutateCSharp.dll <command> [args]
```

or 
```sh
artifacts/MutateCSharp/bin/Release/net8.0/MutateCSharp <command> [args]
```


## Usage







