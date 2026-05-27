FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["NguyenBinhMinh_FBPageAPI/NguyenBinhMinh_FBPageAPI.csproj", "NguyenBinhMinh_FBPageAPI/"]
RUN dotnet restore "NguyenBinhMinh_FBPageAPI/NguyenBinhMinh_FBPageAPI.csproj"

COPY . .

WORKDIR "/src/NguyenBinhMinh_FBPageAPI"
RUN dotnet publish "NguyenBinhMinh_FBPageAPI.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

EXPOSE 3001
ENV ASPNETCORE_URLS=http://+:3001

COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "NguyenBinhMinh_FBPageAPI.dll"]