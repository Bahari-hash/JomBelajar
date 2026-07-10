#!/bin/bash

set -e

echo "restoring server dependencies..."
cd /workspace/server
dotnet clean && dotnet restore
echo "done!"

echo "installing app dependencies..."
cd /workspace/app
rm -rf node_modules && pnpm install
echo "done!"

echo "install admin dependencies..."
cd /workspace/admin
rm -rf node_modules && pnpm install
echo "done!"

echo "all of the project initialized successfully"
