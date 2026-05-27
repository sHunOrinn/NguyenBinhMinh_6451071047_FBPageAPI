FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["RetryService/RetryService.csproj", "RetryService/"]
RUN dotnet restore "RetryService/RetryService.csproj"

COPY . .

WORKDIR "/src/RetryService"
RUN dotnet publish "RetryService.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

EXPOSE 3003
ENV ASPNETCORE_URLS=http://+:3003

COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "RetryService.dll"]