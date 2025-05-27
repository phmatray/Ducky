# Ducky Blazor Demo - Docker Setup

This guide explains how to run the Ducky Blazor WebAssembly demo application using Docker.

## Prerequisites

- Docker Desktop installed
- Docker Compose (usually included with Docker Desktop)

## Quick Start

1. **Build and run the application:**
   ```bash
   docker-compose up --build
   ```

2. **Access the application:**
   Open your browser and navigate to http://localhost:8080

3. **Stop the application:**
   ```bash
   docker-compose down
   ```

## Docker Configuration

### Services

- **ducky-blazor**: The main Blazor WebAssembly application served by nginx
- **redis**: Optional Redis cache for future distributed state management

### Ports

- **8080**: Web application (http://localhost:8080)
- **6379**: Redis (if needed for development)

## Building for Production

1. **Build the Docker image:**
   ```bash
   docker build -f src/demo/Demo.BlazorWasm/Dockerfile -t ducky-blazor:latest .
   ```

2. **Run the container:**
   ```bash
   docker run -d -p 8080:80 --name ducky-app ducky-blazor:latest
   ```

3. **View logs:**
   ```bash
   docker logs ducky-app
   ```

## Development Tips

### Hot Reload
For development with hot reload, run the application locally:
```bash
dotnet watch run --project src/demo/Demo.BlazorWasm
```

### Environment Variables
You can pass environment variables to the container:
```bash
docker run -d -p 8080:80 \
  -e ASPNETCORE_ENVIRONMENT=Development \
  ducky-blazor:latest
```

### Volume Mounting
To persist data or configurations:
```yaml
volumes:
  - ./config:/usr/share/nginx/html/config
```

## Optimization

The Docker image is optimized for:
- Small size using multi-stage builds
- Fast loading with nginx gzip compression
- Security headers for production use
- Proper caching headers for static assets
- PWA support with service worker

## Troubleshooting

1. **Port already in use:**
   Change the port in docker-compose.yml:
   ```yaml
   ports:
     - "8081:80"  # Change 8080 to 8081
   ```

2. **Build failures:**
   Clear Docker cache:
   ```bash
   docker-compose build --no-cache
   ```

3. **Container won't start:**
   Check logs:
   ```bash
   docker-compose logs ducky-blazor
   ```

## Production Deployment

For production deployment, consider:
- Using a reverse proxy (nginx, Traefik)
- Adding SSL/TLS certificates
- Setting up health checks
- Implementing proper logging
- Using container orchestration (Kubernetes, Swarm)

## Additional Features

The containerized application includes:
- PWA support (offline capability)
- Responsive design
- Dark mode support
- Redux DevTools integration
- State persistence
- Cross-tab synchronization