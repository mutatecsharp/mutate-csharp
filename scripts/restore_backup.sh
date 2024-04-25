#!/bin/bash

set -uex

# Take the SUT directory path as input
SUT_PATH=$(realpath "$1")
BACKUP_PATH="$SUT_PATH/.mutate-csharp"

# Ensure script in run from SUT root directory
pushd "$SUT_PATH"

# Verify backup folder exists
test -d "$BACKUP_PATH"

# Delete all content inside the SUT directory except the backup folder
find "$SUT_PATH" -mindepth 1 -maxdepth 1 \
  \( -path "$BACKUP_PATH" -prune \) -o \
   \( -type f -exec rm -f {} \; -o \
   -type d -exec rm -rf {} \; \)

# Move all content in the backup folder to the root SUT directory
cd "$BACKUP_PATH"
shopt -s dotglob
mv -- * ..
shopt -u dotglob

rm -r "$BACKUP_PATH"

popd