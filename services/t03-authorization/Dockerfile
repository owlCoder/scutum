FROM mcr.microsoft.com/dotnet/sdk:8.0 AS test
WORKDIR /app
COPY . .
RUN dotnet restore Scutum.T03.sln && dotnet build Scutum.T03.sln --no-restore --configuration Release
CMD ["sh", "-c", "dotnet test Scutum.T03.sln --no-build --configuration Release --logger 'trx;LogFileName=security.trx' --results-directory /results && dotnet run --project src/Scutum.T03.ConsoleUi --configuration Release --no-build"]

# Zadržan infrastrukturni health scaffold iz PR2; nije authorization API.
# Izvršiva KT1 autorizacija je ConsoleUi + application handler, pokrenuta u test stage-u.
FROM busybox:1.36 AS runtime
RUN mkdir /www && echo OK > /www/health
EXPOSE 8080
CMD ["httpd", "-f", "-p", "8080", "-h", "/www"]
