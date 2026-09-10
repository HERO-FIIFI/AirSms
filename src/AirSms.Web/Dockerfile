FROM node:22-alpine AS build
WORKDIR /app

COPY package*.json ./
RUN npm ci

COPY . .
RUN npm run build

FROM nginx:1.27-alpine
COPY nginx.conf /etc/nginx/conf.d/default.conf
COPY docker-entrypoint.sh /docker-entrypoint.d/40-airsms-env.sh
RUN chmod +x /docker-entrypoint.d/40-airsms-env.sh
COPY --from=build /app/dist /usr/share/nginx/html
EXPOSE 80
