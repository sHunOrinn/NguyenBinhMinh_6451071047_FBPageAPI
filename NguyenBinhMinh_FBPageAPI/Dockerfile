FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY NguyenBinhMinh_FBPageAPI.csproj ./
RUN dotnet restore

COPY . ./
RUN dotnet publish NguyenBinhMinh_FBPageAPI.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:3001
EXPOSE 3001

ENTRYPOINT ["dotnet", "NguyenBinhMinh_FBPageAPI.dll"]