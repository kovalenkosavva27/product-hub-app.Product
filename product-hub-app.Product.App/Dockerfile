#See https://aka.ms/customizecontainer to learn how to customize your debug container and how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/aspnet:6.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build
WORKDIR /src
COPY ["product-hub-app.Product.App/product-hub-app.Product.App.csproj", "product-hub-app.Product.App/"]
RUN dotnet restore "product-hub-app.Product.App/product-hub-app.Product.App.csproj"
COPY . .
WORKDIR "/src/product-hub-app.Product.App"
RUN dotnet build "product-hub-app.Product.App.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "product-hub-app.Product.App.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "product-hub-app.Product.App.dll"]