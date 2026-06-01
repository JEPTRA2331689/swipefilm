FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build

WORKDIR /src

COPY . .

RUN dotnet restore swipefilm/swipefilm/swipefilm.csproj

RUN dotnet publish \
    swipefilm/swipefilm/swipefilm.csproj \
    -c Release \
    -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0

WORKDIR /app

COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:3000

EXPOSE 3000

ENTRYPOINT ["dotnet", "swipefilm.dll"]