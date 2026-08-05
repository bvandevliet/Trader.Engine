FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine
EXPOSE 80

RUN apk add -U --no-cache tzdata wget
RUN addgroup -g 1000 -S appgroup && adduser -u 1000 -S appuser -G appgroup

WORKDIR /app
COPY bin/Release/net10.0/publish .
USER appuser

ENV ASPNETCORE_ENVIRONMENT=Production
ENTRYPOINT ["dotnet", "TraderEngine.Web.dll"]
