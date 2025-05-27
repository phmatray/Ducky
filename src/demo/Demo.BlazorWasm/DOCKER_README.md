# Ducky Blazor WebAssembly - Docker Deployment

This directory contains the necessary files to run the Ducky Blazor WebAssembly demo application using Docker.

## Quick Start

### Build and Run
```bash
# Build the Docker image
docker build -f src/demo/Demo.BlazorWasm/Dockerfile -t ducky-blazor:latest .

# Run the container
docker run -d -p 8080:80 --name ducky-blazor ducky-blazor:latest

# Access the application at http://localhost:8080
```

### Using Docker Compose
```bash
# Start the application
docker compose -f src/demo/Demo.BlazorWasm/docker-compose.yml up -d

# Stop the application
docker compose -f src/demo/Demo.BlazorWasm/docker-compose.yml down
```

## Architecture

The Docker setup uses a multi-stage build:

1. **Build Stage**: Uses `mcr.microsoft.com/dotnet/sdk:9.0` to compile the Blazor WebAssembly application
2. **Runtime Stage**: Uses `nginx:alpine` to serve the static files

## Configuration

### Environment Variables
- Port mapping: The application runs on port 80 inside the container
- Health checks: Built-in health endpoint at `/health`

### PWA Features
The application includes Progressive Web App (PWA) support:
- Service Worker for offline caching
- Web App Manifest for installation
- App icons and splash screens

## Troubleshooting

### Common Issues

**Build fails with NETSDK1147 (missing wasm-tools)**
- The Dockerfile disables AOT compilation to avoid requiring wasm-tools workload
- For production builds with AOT, uncomment the workload installation

**Build fails with missing project references**
- Ensure you're running the build from the repository root
- The Dockerfile copies all necessary build configuration files

### Performance Notes
- AOT compilation is disabled in Docker builds for faster build times
- For production deployments, consider enabling AOT for better runtime performance
- The nginx configuration includes gzip compression for static assets

## Development

To modify the application and rebuild:
```bash
# Make your changes to the source code
# Rebuild the image
docker build -f src/demo/Demo.BlazorWasm/Dockerfile -t ducky-blazor:latest .

# Run with new changes
docker run -d -p 8080:80 ducky-blazor:latest
```