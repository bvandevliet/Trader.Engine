ARG TAG=6.0-alpine

FROM mcr.microsoft.com/dotnet/runtime:${TAG} AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:${TAG} AS build
WORKDIR /src
COPY . .
RUN dotnet restore "TraderEngine.CLI/TraderEngine.CLI.csproj"

WORKDIR /src/TraderEngine.CLI
ARG MODE=Release
RUN dotnet build "TraderEngine.CLI.csproj" -c ${MODE} -o /app/build

FROM build AS publish
ARG MODE=Release
RUN dotnet publish "TraderEngine.CLI.csproj" -c ${MODE} -o /app/publish /p:UseAppHost=false

FROM base AS final
COPY "TraderEngine.CLI/.assets/container/root/" /
RUN apk add -U --no-cache tzdata
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["/bin/sh", "-c" , "crond -f"]