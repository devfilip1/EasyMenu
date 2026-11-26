# Production image of the EasyMenu front (research R15 and R18).
#
# The API client is generated from contracts/openapi.yaml, which lives outside this build
# context, so it is passed as the named build context `contracts`. From frontend/:
#
#   docker build --build-context contracts=../contracts -t easymenu-web .
#   docker run -p 8080:80 -e API_UPSTREAM=http://api:8080 easymenu-web
#
# nginx serves the SPA and proxies /api/, /hubs/ and /media/ to API_UPSTREAM (same origin).
# API_UPSTREAM has no default: the container refuses to start without it.

FROM node:24-alpine AS build
WORKDIR /app/frontend

COPY package.json package-lock.json ./
RUN npm ci

# ng-openapi-gen reads ../contracts/openapi.yaml (see ng-openapi-gen.json).
COPY --from=contracts openapi.yaml /app/contracts/openapi.yaml
COPY . .
RUN npm run build

FROM nginx:alpine

COPY --chmod=755 nginx/05-require-api-upstream.sh /docker-entrypoint.d/05-require-api-upstream.sh
COPY nginx/default.conf.template /etc/nginx/templates/default.conf.template
COPY --from=build /app/frontend/dist/frontend/browser /usr/share/nginx/html

EXPOSE 80
