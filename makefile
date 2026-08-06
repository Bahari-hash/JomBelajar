.PHONY: dev
dev: server-dev app-dev admin-dev

server-dev:
	cd server && dotnet run --project TinyLang --urls https://localhost:7000

app-dev:
	cd app && pnpm dev

admin-dev:
	cd admin && pnpm dev
