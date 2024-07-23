FROM mcr.microsoft.com/dotnet/runtime:8.0-alpine AS base
WORKDIR /app

FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:8.0-alpine AS build
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
RUN addgroup -S appgroup && adduser -S appuser -G appgroup
WORKDIR /app
COPY --from=publish /app/publish .
USER appuser
ENTRYPOINT ["/bin/sh", "-c" , "crond -f"]