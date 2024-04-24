#!/usr/bin/env bash

set -uex

# Take the SUT directory path as input
SUT_PATH=$1
BACKUP_PATH="$SUT_PATH/.mutate-csharp"

# Ensure script in run from SUT root directory
pushd "$SUT_PATH"

# Verify backup folder does not exist
test ! -d "$BACKUP_PATH"

# Copy all content in the root SUT directory to the backup folder
rsync -av "." "./.mutate-csharp"

popd