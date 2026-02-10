FROM mcr.microsoft.com/dotnet/runtime:10.0-alpine

RUN apk add -U --no-cache tzdata
COPY .assets/crontabs/root /etc/crontabs/

WORKDIR /app
COPY bin/Release/net10.0/publish .

ENV ASPNETCORE_ENVIRONMENT=Production
ENTRYPOINT ["/bin/sh", "-c" , "crond -f"]