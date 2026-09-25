# syntax=docker/dockerfile:1

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY Auditart.sln ./
COPY src/Auditart.Domain/Auditart.Domain.csproj src/Auditart.Domain/
COPY src/Auditart.Application/Auditart.Application.csproj src/Auditart.Application/
COPY src/Auditart.Infrastructure/Auditart.Infrastructure.csproj src/Auditart.Infrastructure/
COPY src/Auditart.Api/Auditart.Api.csproj src/Auditart.Api/
RUN dotnet restore src/Auditart.Api/Auditart.Api.csproj

COPY src/ ./src/
RUN dotnet publish src/Auditart.Api/Auditart.Api.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production
EXPOSE 8080
COPY --from=build /app/publish .
USER app
ENTRYPOINT ["dotnet", "Auditart.Api.dll"]
