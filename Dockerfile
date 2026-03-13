# Use the official .NET 10 SDK image as the build environment
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS base
WORKDIR /app

# Build stage
FROM base AS build
COPY src/ ./src/
WORKDIR /app/src/Postech.Users.Api
RUN dotnet restore Postech.Users.Api.csproj
RUN dotnet build Postech.Users.Api.csproj -c Release -o /app/build

# Test stage
FROM build AS test
WORKDIR /app/src/Postech.Users.Api.Tests
RUN dotnet test Postech.Users.Api.Tests.csproj --no-build --verbosity normal

# Publish stage
FROM build AS publish
WORKDIR /app/src/Postech.Users.Api
RUN dotnet publish Postech.Users.Api.csproj -c Release -o /app/publish --no-restore

# Final stage: runtime-only image
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=publish /app/publish .

# Expose port (change if needed)
EXPOSE 80

# Start the application
ENTRYPOINT ["dotnet", "Postech.Users.Api.dll"]
