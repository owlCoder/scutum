# T28 — višefazni build. Stage 'test' koristi profil 'tests' (CI), stage 'runtime' koristi compose.
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY . .
RUN dotnet restore Scutum.T28.sln
RUN dotnet publish src/Scutum.T28.Api/Scutum.T28.Api.csproj -c Release -o /app --no-restore

FROM build AS test
ENTRYPOINT ["dotnet", "test", "Scutum.T28.sln", "-c", "Release", "--logger", "trx;LogFileName=t28.trx", "--results-directory", "/results"]

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
# servis ne radi kao root (least privilege)
USER app
ENTRYPOINT ["dotnet", "Scutum.T28.Api.dll"]
