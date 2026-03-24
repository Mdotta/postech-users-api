# Build context: parent folder that contains both postech-users-api and postech-shared (e.g. projeto2).
# Example: docker build -f postech-users-api/Dockerfile .

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS base
WORKDIR /repo

FROM base AS build
COPY postech-shared ./postech-shared
COPY postech-users-api/src ./postech-users-api/src
WORKDIR /repo/postech-users-api/src/Postech.Users.Api
RUN dotnet restore Postech.Users.Api.csproj
RUN dotnet build Postech.Users.Api.csproj -c Release -o /app/build

FROM build AS test
WORKDIR /repo/postech-users-api/src/Postech.Users.Api.Tests
RUN dotnet test Postech.Users.Api.Tests.csproj -c Release --verbosity normal

FROM build AS publish
WORKDIR /repo/postech-users-api/src/Postech.Users.Api
RUN dotnet publish Postech.Users.Api.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=publish /app/publish .
EXPOSE 80
ENTRYPOINT ["dotnet", "Postech.Users.Api.dll"]
