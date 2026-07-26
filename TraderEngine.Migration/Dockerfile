FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine

RUN apk add -U --no-cache tzdata
RUN addgroup -S appgroup && adduser -S appuser -G appgroup

WORKDIR /app
COPY bin/Release/net10.0/publish .
USER appuser

ENV DOTNET_ENVIRONMENT=Production
ENTRYPOINT ["dotnet", "TraderEngine.Migration.dll"]
