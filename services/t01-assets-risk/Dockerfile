# T01 — višefazni build. Stage 'test' koristi profil 'tests' (CI), stage 'runtime' koristi compose.
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY . .
RUN dotnet restore Scutum.T01.sln
RUN dotnet publish src/Scutum.T01.Api/Scutum.T01.Api.csproj -c Release -o /app --no-restore

FROM build AS test
ENTRYPOINT ["dotnet", "test", "Scutum.T01.sln", "-c", "Release", "--logger", "trx;LogFileName=t01.trx", "--results-directory", "/results"]

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
# servis ne radi kao root (least privilege)
USER app
ENTRYPOINT ["dotnet", "Scutum.T01.Api.dll"]
