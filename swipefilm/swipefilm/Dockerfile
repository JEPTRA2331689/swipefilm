# 1. Define the image arguments at the very top
ARG RUNTIME_IMAGE=://microsoft.com
ARG SDK_IMAGE=://microsoft.com

# 2. Reference the arguments in your FROM statements
FROM ${RUNTIME_IMAGE} AS base
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

FROM ${SDK_IMAGE} AS build
WORKDIR /src
COPY ["swipefilm.csproj", "./"]
RUN dotnet restore "swipefilm.csproj"
COPY . .
RUN dotnet build "swipefilm.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "swipefilm.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "MyAppName.dll"]
