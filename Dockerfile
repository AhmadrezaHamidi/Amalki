# Builds and runs the site (Amlak.Web) on ASP.NET Core 9. Used by docker-compose.yml.
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY Amlak.sln ./
COPY src/Amlak.Domain/Amlak.Domain.csproj src/Amlak.Domain/
COPY src/Amlak.Application/Amlak.Application.csproj src/Amlak.Application/
COPY src/Amlak.Infrastructure/Amlak.Infrastructure.csproj src/Amlak.Infrastructure/
COPY src/Amlak.Web/Amlak.Web.csproj src/Amlak.Web/
RUN dotnet restore src/Amlak.Web/Amlak.Web.csproj
COPY src/ src/
RUN dotnet publish src/Amlak.Web/Amlak.Web.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:9.0
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production
EXPOSE 8080
USER app
ENTRYPOINT ["dotnet", "Amlak.Web.dll"]
