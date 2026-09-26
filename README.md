# SwipeFilm

Trouvez votre prochain film ou série, sans effort.

## Installation

### Prérequis
- [Docker](https://docs.docker.com/get-docker/) installé
- Une clé API TMDB gratuite → [themoviedb.org/settings/api](https://www.themoviedb.org/settings/api)

### Démarrage rapide

```bash
# 1. Télécharger le fichier de configuration
curl -o docker-compose.yml https://raw.githubusercontent.com/JEPTRA2331689/swipefilm/main/docker-compose.yml

# 2. Créer le fichier .env
curl -o .env https://raw.githubusercontent.com/JEPTRA2331689/swipefilm/main/.env.example

# 3. Éditer .env avec vos valeurs
nano .env   # ou notepad .env sur Windows

# 4. Lancer
docker compose up -d
```

L'interface est accessible sur **http://localhost:8096**

---

### Variables d'environnement

| Variable | Description | Requis |
|---|---|---|
| `POSTGRES_PASSWORD` | Mot de passe PostgreSQL | ✅ |
| `JWT_SECRET` | Clé secrète JWT (chaîne aléatoire longue) | ✅ |
| `TMDB_API_KEY` | Clé API The Movie Database | ✅ |
| `PORT` | Port d'accès (défaut: `8096`) | ❌ |

### Mise à jour

```bash
docker compose pull
docker compose up -d
```

### Arrêter

```bash
docker compose down        # arrête (données conservées)
docker compose down -v     # arrête + supprime les données
```

---

## Support

Ouvrir une [issue GitHub](https://github.com/JEPTRA2331689/swipefilm/issues).
