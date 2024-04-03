set -uex

# Ensures script is run from mutate-csharp root directory
pushd "$MUTATE_CSHARP_ROOT"

# Remove all mutate-csharp build artifacts
dotnet clean "$MUTATE_CSHARP_SOLUTION"

# Remove all example build artifacts
find "$MUTATE_CSHARP_EXAMPLES_DIR" -type f -iname '*.sln' -exec dotnet clean {} ';'

popd