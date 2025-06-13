#!/bin/bash

echo "Fixing IDE errors..."

# 1. Clear NuGet caches
echo "Clearing NuGet caches..."
dotnet nuget locals all --clear

# 2. Kill any hanging dotnet processes
echo "Killing hanging dotnet processes..."
pkill -f dotnet || true

# 3. Clear IDE caches
echo "Clearing IDE caches..."
rm -rf ~/.omnisharp 2>/dev/null || true
rm -rf .idea 2>/dev/null || true

# 4. Restore packages
echo "Restoring packages..."
dotnet restore --force

# 5. Build without analyzers first
echo "Building without analyzers..."
dotnet build -p:AnalysisMode=None

# 6. Rebuild with analyzers
echo "Rebuilding with analyzers..."
dotnet build

# 7. For JetBrains Rider specifically
echo "Invalidating Rider caches..."
# Remove Rider cache files
rm -rf ~/Library/Caches/JetBrains/Rider* 2>/dev/null || true

echo "Done! Please restart your IDE and wait for it to re-index the solution."
echo ""
echo "If errors persist:"
echo "1. In Rider: File -> Invalidate Caches... -> Invalidate and Restart"
echo "2. Check that all projects have the same .NET SDK version (9.0.301)"
echo "3. Ensure Directory.Packages.props is properly configured"