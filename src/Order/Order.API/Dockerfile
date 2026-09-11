FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY src/Order/Order.API/Order.API.csproj src/Order/Order.API/
COPY src/Order/Order.Application/Order.Application.csproj src/Order/Order.Application/
COPY src/Order/Order.Infrastructure/Order.Infrastructure.csproj src/Order/Order.Infrastructure/
COPY src/Order/Order.Domain/Order.Domain.csproj src/Order/Order.Domain/
COPY src/Shared/Shared.Contracts/Shared.Contracts.csproj src/Shared/Shared.Contracts/
RUN dotnet restore src/Order/Order.API/Order.API.csproj

COPY src/Order/Order.API/ src/Order/Order.API/
COPY src/Order/Order.Application/ src/Order/Order.Application/
COPY src/Order/Order.Infrastructure/ src/Order/Order.Infrastructure/
COPY src/Order/Order.Domain/ src/Order/Order.Domain/
COPY src/Shared/Shared.Contracts/ src/Shared/Shared.Contracts/

RUN dotnet publish src/Order/Order.API/Order.API.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "Order.API.dll"]
