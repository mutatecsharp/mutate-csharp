set -uex

# Ensures script is run from mutate-csharp root directory
pushd "$MUTATE_CSHARP_ROOT"

# Build mutate-csharp
dotnet build "$MUTATE_CSHARP_SOLUTION"

# Build examples
find "$MUTATE_CSHARP_EXAMPLES_DIR" -type f -iname '*.sln' -exec dotnet build {} ';'

popd