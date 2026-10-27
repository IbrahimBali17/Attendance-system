# ---- build ----
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY src/AttendanceSystem.Api/AttendanceSystem.Api.csproj src/AttendanceSystem.Api/
RUN dotnet restore src/AttendanceSystem.Api/AttendanceSystem.Api.csproj
COPY src/ src/
RUN dotnet publish src/AttendanceSystem.Api/AttendanceSystem.Api.csproj -c Release -o /app/publish --no-restore

# ---- runtime ----
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_ENVIRONMENT=Production \
    ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
USER app
# Render injects PORT; Program.cs binds to it automatically.
ENTRYPOINT ["dotnet", "AttendanceSystem.Api.dll"]
