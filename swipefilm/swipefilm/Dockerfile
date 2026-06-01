# Base image for running the app
FROM ://microsoft.com AS base
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

# SDK image for building the source code
FROM ://microsoft.com AS build
WORKDIR /src
COPY ["swipefilm.csproj", "./"]
RUN dotnet restore "swipefilm.csproj"
COPY . .
RUN dotnet build "swipefilm.csproj" -c Release -o /app/build

# Publish stage
FROM build AS publish
RUN dotnet publish "swipefilm.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Final production stage
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "swipefilm.dll"]