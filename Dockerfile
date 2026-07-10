# ── Build stage ──────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy project files first (layer caching for restores)
COPY ZenBotCS.sln .
COPY ZenBotCS/ZenBotCS.csproj ZenBotCS/
COPY ZenBotCS.Entities/ZenBotCS.Entities.csproj ZenBotCS.Entities/
COPY ZenBotCS.Web/ZenBotCS.Web.csproj ZenBotCS.Web/
COPY ZenBotCS.Tests/ZenBotCS.Tests.csproj ZenBotCS.Tests/
RUN dotnet restore

# Copy the rest and publish both apps
COPY . .
RUN dotnet publish ZenBotCS/ZenBotCS.csproj -c Release -o /out/bot
RUN dotnet publish ZenBotCS.Web/ZenBotCS.Web.csproj -c Release -o /out/web

# ── Bot runtime image ────────────────────────────────────
FROM mcr.microsoft.com/dotnet/runtime:8.0 AS bot
WORKDIR /app
COPY --from=build /out/bot .
ENTRYPOINT ["dotnet", "ZenBotCS.dll"]

# ── Web runtime image ────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS web
WORKDIR /app
COPY --from=build /out/web .
# SkiaSharp needs libfontconfig1 for the bundled .ttf fonts
RUN apt-get update && apt-get install -y --no-install-recommends \
    libfontconfig1 \
    && rm -rf /var/lib/apt/lists/*
ENTRYPOINT ["dotnet", "ZenBotCS.Web.dll"]