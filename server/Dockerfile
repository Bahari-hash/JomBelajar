FROM mcr.microsoft.com/dotnet/aspnet:10.0.9 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0.301 AS build
WORKDIR /src

COPY ["Directory.Build.props", "./"]
COPY ["Directory.Packages.props", "./"]
COPY ["global.json", "./"]

COPY ["TinyLang/TinyLang.csproj", "TinyLang/"]
RUN dotnet restore "TinyLang/TinyLang.csproj"

COPY TinyLang TinyLang

WORKDIR /src/TinyLang
RUN dotnet build "TinyLang.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "TinyLang.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT [ "dotnet", "TinyLang.dll" ]
