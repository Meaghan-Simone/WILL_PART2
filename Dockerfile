# Build:  docker build -t ug-crm .
# Run:    docker run -p 8080:8080 -v ug-data:/data -e ASPNETCORE_ENVIRONMENT=Production ug-crm
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY UncoveringGreatnessCRM.csproj ./
RUN dotnet restore UncoveringGreatnessCRM.csproj
COPY . .
RUN dotnet publish UncoveringGreatnessCRM.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app
COPY --from=build /app .
# SQLite file and Data Protection keys live on a volume so they survive restarts and redeploys
ENV ASPNETCORE_URLS=http://+:8080 \
    ConnectionStrings__Default="Data Source=/data/crm.db" \
    DataProtection__KeyPath=/data/keys
RUN mkdir -p /data /app/App_Data && chown -R $APP_UID /data /app/App_Data
VOLUME /data
EXPOSE 8080
USER $APP_UID
ENTRYPOINT ["dotnet", "UncoveringGreatnessCRM.dll"]
