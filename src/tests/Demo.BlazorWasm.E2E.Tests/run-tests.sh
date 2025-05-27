#!/bin/bash

# Colors for output
GREEN='\033[0;32m'
RED='\033[0;31m'
NC='\033[0m' # No Color

echo "🚀 Starting E2E tests for Ducky Blazor WebAssembly"

# Check if Docker is running
if ! docker info > /dev/null 2>&1; then
    echo -e "${RED}Docker is not running. Please start Docker first.${NC}"
    exit 1
fi

# Build and start the application
echo "📦 Building Docker image..."
cd ../../.. # Go to repo root
docker build -f src/demo/Demo.BlazorWasm/Dockerfile -t ducky-blazor:e2e-test . || exit 1

# Stop any existing container
docker stop ducky-blazor-e2e 2>/dev/null || true
docker rm ducky-blazor-e2e 2>/dev/null || true

# Start container
echo "🐳 Starting Docker container..."
docker run -d --name ducky-blazor-e2e -p 8080:80 ducky-blazor:e2e-test || exit 1

# Wait for container to be ready
echo "⏳ Waiting for application to be ready..."
attempts=0
max_attempts=30
while ! curl -s http://localhost:8080 > /dev/null; do
    attempts=$((attempts + 1))
    if [ $attempts -ge $max_attempts ]; then
        echo -e "${RED}Application failed to start after $max_attempts seconds${NC}"
        docker logs ducky-blazor-e2e
        docker stop ducky-blazor-e2e
        docker rm ducky-blazor-e2e
        exit 1
    fi
    sleep 1
done

echo -e "${GREEN}✅ Application is ready!${NC}"

# Install Playwright browsers if needed
echo "🎭 Installing Playwright browsers..."
cd src/tests/Demo.BlazorWasm.E2E.Tests
dotnet build
pwsh bin/Debug/net9.0/playwright.ps1 install chromium || exit 1

# Run tests
echo "🧪 Running E2E tests..."
export BASE_URL=http://localhost:8080
dotnet test --logger "console;verbosity=normal"
TEST_RESULT=$?

# Cleanup
echo "🧹 Cleaning up..."
docker stop ducky-blazor-e2e
docker rm ducky-blazor-e2e

# Report results
if [ $TEST_RESULT -eq 0 ]; then
    echo -e "${GREEN}✅ All tests passed!${NC}"
else
    echo -e "${RED}❌ Some tests failed!${NC}"
fi

exit $TEST_RESULT