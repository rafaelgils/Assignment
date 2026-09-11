FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY src/Gateway/Gateway.API/Gateway.API.csproj src/Gateway/Gateway.API/
COPY src/Shared/Shared.Contracts/Shared.Contracts.csproj src/Shared/Shared.Contracts/
RUN dotnet restore src/Gateway/Gateway.API/Gateway.API.csproj

COPY src/Gateway/Gateway.API/ src/Gateway/Gateway.API/
COPY src/Shared/Shared.Contracts/ src/Shared/Shared.Contracts/

RUN dotnet publish src/Gateway/Gateway.API/Gateway.API.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "Gateway.API.dll"]
