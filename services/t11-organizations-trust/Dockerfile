# ---- build ----
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /app
COPY src/ ./src/
RUN dotnet publish src/T11.Api/T11.Api.csproj -c Release -o /out

# ---- runtime (the service) ----
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:8080
COPY --from=build /out .
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "T11.Api.dll"]

# ---- test (used by the `tests` profile) ----
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS test
WORKDIR /app
COPY src/ ./src/
COPY tests/ ./tests/
RUN dotnet restore tests/T11.Tests/T11.Tests.csproj
CMD ["dotnet", "test", "tests/T11.Tests/T11.Tests.csproj", "--no-restore", \
     "--logger", "trx;LogFileName=results.trx", \
     "--results-directory", "/results"]
