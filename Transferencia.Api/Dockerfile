FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY BancoAna.sln ./
COPY Transferencia.Api/Transferencia.Api.csproj Transferencia.Api/
COPY ContaCorrente.Domain/ContaCorrente.Domain.csproj ContaCorrente.Domain/
COPY ContaCorrente.Infrastructure/ContaCorrente.Infrastructure.csproj ContaCorrente.Infrastructure/
RUN dotnet restore Transferencia.Api/Transferencia.Api.csproj
COPY . .
RUN dotnet publish Transferencia.Api/Transferencia.Api.csproj -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
RUN mkdir -p /app/data
ENTRYPOINT ["dotnet", "Transferencia.Api.dll"]

