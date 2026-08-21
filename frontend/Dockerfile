# Build stage: produce the optimized Angular bundle.
FROM node:22-alpine AS build
WORKDIR /src

# Copy manifests first so `npm ci` is cached independently of source changes.
COPY package.json package-lock.json ./
RUN npm ci

COPY . .
RUN npm run build

# Runtime stage: nginx serves the bundle and reverse-proxies /api to the API.
FROM nginx:alpine AS runtime
COPY nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /src/dist/example-com-queue-frontend/browser /usr/share/nginx/html
EXPOSE 80
