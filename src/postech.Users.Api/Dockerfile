FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["postech.Users.Api/postech.Users.Api.csproj", "postech.Users.Api/"]
RUN dotnet restore "postech.Users.Api/postech.Users.Api.csproj"
COPY . .
WORKDIR "/src/postech.Users.Api"
RUN dotnet build "postech.Users.Api.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "postech.Users.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "postech.Users.Api.dll"]