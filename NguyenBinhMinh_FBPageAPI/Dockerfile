# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy csproj and restore
COPY NguyenBinhMinh_FBPageAPI/*.csproj NguyenBinhMinh_FBPageAPI/
WORKDIR /src/NguyenBinhMinh_FBPageAPI
RUN dotnet restore

# Copy everything else and publish
WORKDIR /src
COPY . .
WORKDIR /src/NguyenBinhMinh_FBPageAPI
RUN dotnet publish -c Release -o /app/publish /p:UseAppHost=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:3001
EXPOSE 3001

ENTRYPOINT ["dotnet", "NguyenBinhMinh_FBPageAPI.dll"]