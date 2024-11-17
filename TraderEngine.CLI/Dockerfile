FROM mcr.microsoft.com/dotnet/runtime:8.0-alpine

RUN apk add -U --no-cache tzdata
COPY .assets/container/root/ /

WORKDIR /app
COPY bin/Release/net8.0/publish .

RUN addgroup -S appgroup && adduser -S appuser -G appgroup
USER appuser

ENV ASPNETCORE_ENVIRONMENT=Production
ENTRYPOINT ["/bin/sh", "-c" , "crond -f"]