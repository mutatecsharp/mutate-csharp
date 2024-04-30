#!/bin/bash

set -uex

# Take the SUT directory path as input
SUT_PATH=$(realpath "$1")
BACKUP_PATH="$SUT_PATH/.mutate-csharp"

# Ensure script in run from SUT root directory
pushd "$SUT_PATH"

# Verify backup folder does not exist
[[ ! -d "$BACKUP_PATH" ]] || exit 1

## Copy all content in the root SUT directory to the backup folder
rsync -av "." "./.mutate-csharp"
echo "Backup completed."

popd