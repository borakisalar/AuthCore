FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ["StajBackendProject.csproj", "./"]
RUN dotnet restore "StajBackendProject.csproj"

COPY . .
RUN dotnet build "StajBackendProject.csproj" -c Release -o /app/build
RUN dotnet publish "StajBackendProject.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "StajBackendProject.dll"]