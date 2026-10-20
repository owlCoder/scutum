# T10 backend (.NET 10). Build kontekst je koren servisa (services/t10-telemetry-detection), jer solution
# obuhvata i src/backend i tests/backend. Stage-ovi po konvenciji repoa:
#   test    — svi .NET testovi; .trx rezultati u /results (profil 'tests', servis t10-tests-api)
#   runtime — Web API na portu 8080, kao neprivilegovan korisnik (servis t10-api)

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY global.json Directory.Build.props Directory.Packages.props Scutum.T10.slnx ./
COPY src/backend/ src/backend/
COPY tests/backend/ tests/backend/
RUN dotnet restore Scutum.T10.slnx
RUN dotnet build Scutum.T10.slnx -c Release --no-restore

FROM build AS test
CMD ["dotnet", "test", "Scutum.T10.slnx", "-c", "Release", "--no-build", "--logger", "trx", "--results-directory", "/results"]

FROM build AS publish
RUN dotnet publish src/backend/Scutum.T10.Api/Scutum.T10.Api.csproj -c Release --no-build -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=publish /app/publish .
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "Scutum.T10.Api.dll"]
