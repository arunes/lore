# ---- Stage 1: Build the React UI ----
FROM node:22-alpine AS ui
WORKDIR /src
COPY Lore.UI/package.json Lore.UI/pnpm-lock.yaml ./
RUN npm install -g pnpm@11.17.0 && pnpm install --frozen-lockfile
COPY Lore.UI/ .
RUN pnpm build

# ---- Stage 2: Publish the .NET API (including the UI static assets) ----
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
COPY --from=ui /src/dist Lore.App/wwwroot
RUN dotnet publish Lore.App/Lore.App.csproj -c Release -o /app/publish

# ---- Stage 3: Runtime ----
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

ENV URLS=http://+:8080
ENV LORE_DATA_ROOT=/app/lore
ENV LORE_IN_CONTAINER=true
# OCR models (shipped via RapidOcrNet at /app/models/v5) and the embedding
# model are copied/kept under the data root on first startup.
EXPOSE 8080

ENTRYPOINT ["dotnet", "Lore.App.dll"]
