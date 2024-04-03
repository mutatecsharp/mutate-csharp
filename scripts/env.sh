set -uex

# Environment variables
MUTATE_CSHARP_ROOT=$(pwd)
MUTATE_CSHARP_SOLUTION="$MUTATE_CSHARP_ROOT/MutateCSharp.sln"
MUTATE_CSHARP_EXAMPLES_DIR="$MUTATE_CSHARP_ROOT/examples"

# Verify this script is ran in root directory of mutate-csharp
test -d "$MUTATE_CSHARP_ROOT"
test -f "$MUTATE_CSHARP_SOLUTION"

# Dependency requirement(s) sanity check
# .NET SDK should be installed
test -n "$(dotnet --list-sdks)"

# Export variables
export MUTATE_CSHARP_ROOT
export MUTATE_CSHARP_SOLUTION
export MUTATE_CSHARP_EXAMPLES_DIR