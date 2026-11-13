# Build Stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /app

# Copy csproj and restore dependencies
COPY src/FinTechEngine.Api/*.csproj ./src/FinTechEngine.Api/
RUN dotnet restore ./src/FinTechEngine.Api/FinTechEngine.Api.csproj

# Copy the rest of the source code and publish
COPY src/FinTechEngine.Api/ ./src/FinTechEngine.Api/
WORKDIR /app/src/FinTechEngine.Api
RUN dotnet publish -c Release -o /app/publish /p:UseAppHost=false

# Runtime Stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
EXPOSE 5000
ENV ASPNETCORE_URLS=http://+:5000
ENV ASPNETCORE_ENVIRONMENT=Production

COPY --from=build /app/publish .

# Directory for SQLite persistent database
VOLUME /app/data

ENTRYPOINT ["dotnet", "FinTechEngine.Api.dll"]
