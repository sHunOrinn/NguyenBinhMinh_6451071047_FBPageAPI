FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["CoreServices/CoreServices.csproj", "CoreServices/"]
RUN dotnet restore "CoreServices/CoreServices.csproj"

COPY . .

WORKDIR "/src/CoreServices"
RUN dotnet publish "CoreServices.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

EXPOSE 3002
ENV ASPNETCORE_URLS=http://+:3002

COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "CoreServices.dll"]