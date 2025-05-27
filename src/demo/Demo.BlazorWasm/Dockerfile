# Build stage
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy global configuration files
COPY ["global.json", "./"]
COPY ["NuGet.Config", "./"]

# Copy build props and package management files
COPY ["src/Directory.Build.props", "src/"]
COPY ["src/Directory.Packages.props", "src/"]
COPY ["src/build/", "src/build/"]

# Copy solution and project files
COPY ["Ducky.sln", "./"]
COPY ["src/library/Ducky/Ducky.csproj", "src/library/Ducky/"]
COPY ["src/library/Ducky.Generator/Ducky.Generator.csproj", "src/library/Ducky.Generator/"]
COPY ["src/library/Ducky.Blazor/Ducky.Blazor.csproj", "src/library/Ducky.Blazor/"]
COPY ["src/demo/Demo.BlazorWasm/Demo.BlazorWasm.csproj", "src/demo/Demo.BlazorWasm/"]

# Restore dependencies (AOT disabled for Docker builds)
RUN dotnet restore "src/demo/Demo.BlazorWasm/Demo.BlazorWasm.csproj" -p:RunAOTCompilation=false

# Copy source code
COPY . .

# Build and publish (disable AOT for faster builds)
WORKDIR "/src/src/demo/Demo.BlazorWasm"
RUN dotnet publish "Demo.BlazorWasm.csproj" -c Release -o /app/publish -p:RunAOTCompilation=false

# Runtime stage - nginx for serving static files
FROM nginx:alpine AS runtime
WORKDIR /usr/share/nginx/html

# Copy published files
COPY --from=build /app/publish/wwwroot .

# Copy nginx configuration
COPY src/demo/Demo.BlazorWasm/nginx.conf /etc/nginx/nginx.conf

# Expose port
EXPOSE 80

# Start nginx
CMD ["nginx", "-g", "daemon off;"]