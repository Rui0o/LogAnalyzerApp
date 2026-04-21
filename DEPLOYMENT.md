# LogAnalyzer — Deployment Guide

## Target Environment
- **OS:** Ubuntu (Linux VM)
- **Resources:** 4 vCPU, 16 GB RAM, 200 GB SSD
- **Stack:** Docker + Docker Compose
- **Instances:** prod (port 5000), test (port 5001)

---

## 1. Files to Add to Repo

### Dockerfile
```dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY . .
RUN dotnet publish -c Release -o /app

FROM base
WORKDIR /app
COPY --from=build /app .
EXPOSE 5000
ENTRYPOINT ["dotnet", "LogAnalyzerApp.dll"]
```

### docker-compose.yml
```yaml
services:
  prod:
    build: .
    ports:
      - "5000:5000"
    environment:
      - ASPNETCORE_URLS=http://0.0.0.0:5000
      - ASPNETCORE_ENVIRONMENT=Production
    restart: always

  test:
    build: .
    ports:
      - "5001:5000"
    environment:
      - ASPNETCORE_URLS=http://0.0.0.0:5000
      - ASPNETCORE_ENVIRONMENT=Production
    restart: always
```

---

## 2. One-Time VM Setup

```bash
# Install Docker
sudo apt update && sudo apt install docker.io docker-compose-v2 -y
sudo usermod -aG docker $USER
# Log out and back in for group change to take effect

# Clone repo
git clone <REPO_URL> loganalyzer
cd loganalyzer

# Start both instances
docker compose up -d

# Open firewall ports
sudo ufw allow 5000
sudo ufw allow 5001
```

---

## 3. Updating the App

```bash
cd loganalyzer
git pull

# Update test first, verify, then prod
docker compose up -d --build test
# Check http://VM_IP:5001 — if OK:
docker compose up -d --build prod
```

---

## 4. Useful Commands

```bash
# Check status
docker compose ps

# View logs
docker compose logs -f prod
docker compose logs -f test

# Restart a single instance
docker compose restart prod

# Stop everything
docker compose down

# Rebuild from scratch (e.g. after Dockerfile changes)
docker compose up -d --build --force-recreate
```

---

## 5. Optional — Future Improvements

- **Reverse proxy (nginx/Caddy):** Put in front for HTTPS, custom domain, cleaner URLs
- **GitHub Actions CI/CD:** Auto-deploy on push to master — build image, SSH to VM, pull and restart
- **Separate branches for test/prod:** Test instance builds from `develop`, prod from `master`
- **Health checks:** Add `healthcheck` to docker-compose.yml for automatic restart on failure
- **Volume mounts:** If temp files or logs need to persist between container restarts

---

## Instructions for Claude

When the user asks to set up deployment:
1. Create the `Dockerfile` and `docker-compose.yml` in the repo root based on the templates above
2. Make sure `appsettings.json` does NOT hardcode `StandaloneUrls` — the `ASPNETCORE_URLS` env var in docker-compose takes precedence
3. Add a `.dockerignore` with `bin/`, `obj/`, `publish/`, `.git/` to speed up builds
4. Do NOT set up HTTPS inside the container — that should be handled by a reverse proxy if needed later
5. The app uses temp files for log downloads (`FileOptions.DeleteOnClose`) — this works fine inside a container, no volume needed
