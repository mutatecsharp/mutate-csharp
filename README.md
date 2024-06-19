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
artifacts/MutateCSharp/bin/Release/net8.0/MutateCSharp <command> [<args>]
```

## Usage

`mutate-csharp` has 5 available commands:
- `mutate`: Applies mutant schemata generation to the program under test to encode all artificial defects.
- `generate-tracer`: Instruments the program under test to track mutant coverage.
- `trace`: Record the mutant coverage given a list of tests. This could be modified to trace mutant coverage of any program specified.
- `test`: Performs mutation analysis by executing all tests against mutants.
- `analyse`: Utility that allows inspection of mutation registry.

```sh
Usage: MutateCSharp <command> [<args>]
  mutate             Commence mutation.

  analyse            Load mutation registry and commence mutation testing.

  generate-tracer    Instrument system under test to trace mutant execution.

  trace              Commence mutant execution tracing.

  test               Commence mutation testing using execution trace
                     information.
```

### Usage for `mutate`
Generates artificial defects and encodes all such defects into the program under test as a metaprogram. 
This creates a _mutation registry_ that contains display information of all mutations encoded in the program 
under test: it is output in the same directory as the specified (solution / project) directory, with the name
`mutation.mucs.json`.

The mutation occurs in place - if you want to check how many mutants are produced, you can enable
the `--restore` flag, which restores the files that are backed up by default to its original state.

Recommended: enable `--omit-redundant` by default to reduce work needed to evaluate equivalent / redundant mutants.

```sh
Usage: MutateCSharp mutate [<args>]

  --solution          The path to C# solution file (.sln).

  --project           The path to C# project file (.csproj).

  --directories       The directories within a C# project containing C# source
                      files.

  --source-file       The path to an individual C# source file (.cs).

  --restore           (Default: false) Restore files to original state after
                      applying mutation testing.

  --omit-redundant    (Default: false) Do not generate equivalent or redundant
                      mutants.

  --dry-run           (Default: false) Perform a dry run.

  --ignore-files      Path(s) to C# source files to ignore (.cs).
```

### Usage for `generate-tracer`
Instruments the program under test with mechanisms that record execution traces for tests.
The configurations for `generate-tracer` should match that of `mutate`.

This should create the same mutation registry as the `mutate` command when the same (solution / project / directory in project / source file)
is specified, but with the name `tracer-registry.mucs.json`.

As with `mutate`, the instrumentation occurs in place. To record the execution trace, see `trace`.

```sh
Usage: MutateCSharp generate-tracer [<args>]

  --solution          The path to C# solution file (.sln).

  --project           The path to C# project file (.csproj).

  --directories       The directories within a C# project containing C# source
                      files.

  --source-file       The path to an individual C# source file (.cs).

  --restore           (Default: false) Restore files to original state after
                      generating mutant execution tracer.

  --omit-redundant    (Default: false) Do not generate equivalent or redundant
                      mutants.

  --dry-run           (Default: false) Perform a dry run.

  --ignore-files      Path(s) to C# source files to ignore (.cs).
```

### Usage for `trace`
Runs the tests to record which mutants are reachable per test. This is not necessary,
but is recommended as it can greatly speed up the mutation testing process.

`trace` requires the existence of the program under test that has been applied with
`generate-tracer`.
```sh
Usage: MutateCSharp trace [<args>]
  --test-project         Required. The directory/path to test project containing
                         the tests.

  --output-directory     Required. The directory to output mutant execution
                         traces.

  --tests-list           The list of tests to trace mutant execution against.

  --test-name            The full name of test to trace mutant execution
                         against.

  --mutation-registry    Required. The path to the mutation registry (.json).

  --tracer-registry      Required. The path to the tracer mutation registry
                         (.json).

  --testrun-settings     The path to the individual test run setting
                         (.runsettings).

  --no-build             (Default: false) Do not build test project.
```

### Usage for `test`
Evaluates each mutant (representing an artificial fault) if it can be detected by
the tests corresponding to the program under test.

`test` requires the mutated version of the program under test to be available.
```sh
Usage: MutateCSharp test [<args>]
  --test-project              Required. The path to the test project.

  --passing-tests             Required. The path to list of passing tests, in
                              order of ascending duration.

  --project                   Optional. The path to the project under test
                              (.csproj).

  --source-file-under-test    Optional. If specified, mutation testing will only
                              focus its efforts to the particular source file.

  --mutation-registry         Required. The path to the mutation registry
                              (.json).

  --tracer-registry           Required. The path to the tracer mutation registry
                              (.json).

  --mutant-traces             The directory to mutant execution trace.

  --specified-mutants         The path to a list of specific mutants to test
                              against.

  --testrun-settings          The path to the individual test run setting
                              (.runsettings).

  --test-output               Required. The output directory to store metadata
                              of tests currently being worked on.

  --killed-mutants-output     Required. The output directory to store metadata
                              of mutants killed by tests.

  --dry-run                   (Default: false) Perform dry run.
```

### Usage for `analyse`
Useful to see how many mutants are generated. 

```sh
Usage: MutateCSharp analyse [<args>]
  --registry    The path to mutation registry (.json).

  --project     The path to mutated C# project file (.csproj).
```

## License
`mutate-csharp` is covered under the terms of the [MIT license](https://en.wikipedia.org/wiki/MIT_License). 
Contributions are welcome!
