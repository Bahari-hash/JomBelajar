FROM tinylang-local-api:latest AS final
WORKDIR /app
COPY publish/ ./
