FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
RUN dotnet restore ./src/IdentityAuthnApi/IdentityAuthnApi.csproj
RUN dotnet publish ./src/IdentityAuthnApi/IdentityAuthnApi.csproj \
    -c Release \
    -o /app/publish \
    --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "IdentityAuthnApi.dll"]

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS test
WORKDIR /src
COPY . .
RUN dotnet restore ./tests/t12-tests.csproj
RUN dotnet build ./tests/t12-tests.csproj -c Release --no-restore
ENTRYPOINT ["dotnet", "test", "./t12-tests/t12-tests.csproj", "-c", "Release", "--no-build", "--logger", "trx;LogFileName=t12-tests.trx", "--results-directory", "/results"]
