# Runs on Render, Railway, Koyeb, Fly.io or any VPS with Docker.
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY Eshhanly.csproj ./
RUN dotnet restore
COPY . .
RUN dotnet publish Eshhanly.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app
# Arabic dates/numbers need ICU (included in the aspnet image).
COPY --from=build /app .
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
USER $APP_UID
ENTRYPOINT ["dotnet", "Eshhanly.dll"]
