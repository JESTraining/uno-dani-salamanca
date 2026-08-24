# "dev" stage: targeted by docker/docker-compose.override.yml, which mounts
# the local source over /app and runs the Angular dev server for hot reload
# instead of the production build below.
FROM node:22-alpine AS dev
WORKDIR /app
COPY package.json package-lock.json ./
RUN npm ci
COPY . .
# Binds the same container-internal port (80) the production Nginx stage
# uses, so docker-compose.override.yml does not need its own "ports" entry -
# Compose concatenates (rather than replaces) list fields on override, so a
# second "ports" mapping here would conflict with the base file's.
CMD ["npm", "start", "--", "--host", "0.0.0.0", "--port", "80"]

FROM node:22-alpine AS build
WORKDIR /app

COPY package.json package-lock.json ./
RUN npm ci

COPY . .
RUN npm run build -- --configuration production

FROM nginx:alpine AS final

COPY nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /app/dist/Frontend/browser /usr/share/nginx/html

EXPOSE 80

HEALTHCHECK --interval=15s --timeout=5s --start-period=10s --retries=3 \
    CMD wget -q --spider http://localhost/ || exit 1
