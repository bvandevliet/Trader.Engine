FROM mcr.microsoft.com/dotnet/aspnet:8.0-alpine AS base
WORKDIR /app
EXPOSE 80

FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:8.0-alpine AS build
WORKDIR /src
COPY . .
RUN dotnet restore "TraderEngine.API/TraderEngine.API.csproj"

WORKDIR /src/TraderEngine.API
ARG MODE=Release
RUN dotnet build "TraderEngine.API.csproj" -c ${MODE} -o /app/build

FROM build AS publish
ARG MODE=Release
RUN dotnet publish "TraderEngine.API.csproj" -c ${MODE} -o /app/publish /p:UseAppHost=false

FROM base AS final
RUN apk add -U --no-cache tzdata
RUN addgroup -S appgroup && adduser -S appuser -G appgroup
WORKDIR /app
COPY --from=publish /app/publish .
USER appuser
ENTRYPOINT ["dotnet", "TraderEngine.API.dll"]