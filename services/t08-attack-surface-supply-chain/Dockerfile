ARG DOTNET_VERSION=10.0

FROM mcr.microsoft.com/dotnet/sdk:${DOTNET_VERSION} AS build
WORKDIR /src
COPY . .
RUN dotnet publish src/T08.Api/T08.Api.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/sdk:${DOTNET_VERSION} AS test
WORKDIR /src
COPY . .
CMD ["dotnet", "test", "--logger", "trx;LogFileName=results.trx", "--results-directory", "/results"]

FROM mcr.microsoft.com/dotnet/aspnet:${DOTNET_VERSION} AS runtime
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
USER app
ENTRYPOINT ["dotnet", "T08.Api.dll"]