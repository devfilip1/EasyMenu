# Production image of the EasyMenu front (research R15).
#
# The API client is generated from contracts/openapi.yaml, which lives outside this build
# context, so it is passed as the named build context `contracts`. From frontend/:
#
#   docker build --build-context contracts=../contracts -t easymenu-web .
#   docker run -p 8080:80 -e API_ORIGIN=http://localhost:5080 -e API_WS_ORIGIN=ws://localhost:5080 easymenu-web

FROM node:24-alpine AS build
WORKDIR /app/frontend

COPY package.json package-lock.json ./
RUN npm ci

# ng-openapi-gen reads ../contracts/openapi.yaml (see ng-openapi-gen.json).
COPY --from=contracts openapi.yaml /app/contracts/openapi.yaml
COPY . .
RUN npm run build

FROM nginx:alpine

# Origins allowed by the CSP (img-src/connect-src). Empty means same origin only.
ENV API_ORIGIN="" \
    API_WS_ORIGIN=""

COPY nginx/default.conf.template /etc/nginx/templates/default.conf.template
COPY --from=build /app/frontend/dist/frontend/browser /usr/share/nginx/html

EXPOSE 80
