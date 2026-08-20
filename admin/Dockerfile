FROM docker.io/library/node:22-alpine AS build
WORKDIR /src

ARG VITE_BASE_PATH=/admin/
ENV VITE_BASE_PATH=$VITE_BASE_PATH

COPY package.json pnpm-lock.yaml pnpm-workspace.yaml ./
RUN corepack enable && pnpm install --frozen-lockfile

COPY . .
RUN pnpm build

FROM docker.io/library/nginx:1.28-alpine
COPY nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /src/dist /usr/share/nginx/html/admin
EXPOSE 80
