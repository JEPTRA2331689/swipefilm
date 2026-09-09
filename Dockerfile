# ─── Frontend — export statique Next.js ─────────────────────────
FROM node:20-alpine AS frontend-build
WORKDIR /app
COPY swipefilmNextjs/swipefilm-web/package.json swipefilmNextjs/swipefilm-web/package-lock.json ./
# ✅ npm ci refuse de tourner si le lockfile n'a pas les entrées optionnelles
# natives (musl/Alpine) pour tailwindcss/lightningcss — npm install les
# résout pour la plateforme courante au lieu d'exiger un lockfile figé.
RUN npm install
COPY swipefilmNextjs/swipefilm-web/ ./
RUN npm run build
# → produit /app/out (output: "export", voir next.config.ts)

# ─── Backend — .NET ──────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS backend-build
WORKDIR /src
COPY swipefilm/swipefilm/ ./swipefilm/swipefilm/
RUN dotnet restore swipefilm/swipefilm/swipefilm.csproj
RUN dotnet publish \
    swipefilm/swipefilm/swipefilm.csproj \
    -c Release \
    -o /app/publish

# ─── Image finale ─────────────────────────────────────────────────
# ✅ Un seul conteneur — le backend sert le frontend statique directement
# (UseStaticFiles + MapFallback dans Program.cs), même pattern que
# Radarr/Sonarr. Plus de serveur Node séparé en prod.
FROM mcr.microsoft.com/dotnet/aspnet:8.0

# ✅ wget n'est pas présent par défaut sur l'image aspnet — nécessaire pour
# le HEALTHCHECK plus bas.
RUN apt-get update \
    && apt-get install -y --no-install-recommends wget \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /app

COPY --from=backend-build /app/publish .
COPY --from=frontend-build /app/out ./wwwroot

ENV ASPNETCORE_URLS=http://+:3000

# ✅ config/ (Jellyfin/Radarr/Sonarr, config/settings.json) et logs/ sont
# créés au démarrage relativement à ce WORKDIR — à monter en volumes pour
# survivre aux redéploiements du conteneur (voir docker-compose.yml).
EXPOSE 3000

HEALTHCHECK --start-period=20s --interval=15s --timeout=3s --retries=3 \
    CMD wget --no-verbose --tries=1 --spider http://localhost:3000/api/setup/status || exit 1

ENTRYPOINT ["dotnet", "swipefilm.dll"]
