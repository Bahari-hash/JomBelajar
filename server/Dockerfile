FROM mcr.microsoft.com/dotnet/aspnet:10.0.9 AS base
WORKDIR /app
EXPOSE 8080
RUN apt-get update \
    && apt-get install --yes --no-install-recommends ffmpeg \
    && rm -rf /var/lib/apt/lists/*

FROM mcr.microsoft.com/dotnet/sdk:10.0.301 AS build
WORKDIR /src

COPY ["Directory.Build.props", "./"]
COPY ["Directory.Packages.props", "./"]
COPY ["global.json", "./"]
COPY ["dotnet-tools.json", "./"]

COPY ["TinyLang/TinyLang.csproj", "TinyLang/"]
RUN dotnet restore "TinyLang/TinyLang.csproj"

COPY TinyLang TinyLang

WORKDIR /src/TinyLang
RUN dotnet build "TinyLang.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "TinyLang.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM build AS migrations
WORKDIR /src
RUN dotnet tool restore
ENTRYPOINT ["dotnet", "ef", "database", "update", "--project", "TinyLang/TinyLang.csproj", "--startup-project", "TinyLang/TinyLang.csproj"]

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT [ "dotnet", "TinyLang.dll" ]
