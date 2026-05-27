FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["BackendAPI/BackendAPI.csproj", "BackendAPI/"]
RUN dotnet restore "BackendAPI/BackendAPI.csproj"

COPY . .

WORKDIR "/src/BackendAPI"
RUN dotnet publish "BackendAPI.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

EXPOSE 3000
ENV ASPNETCORE_URLS=http://+:3000

COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "BackendAPI.dll"]