FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["Directory.Build.props", "./"]
COPY ["src/Server/VK.Web/VK.Web.csproj", "src/Server/VK.Web/"]
COPY ["src/Server/VK.Core/VK.Core.csproj", "src/Server/VK.Core/"]
COPY ["src/Server/VK.Infrastructure/VK.Infrastructure.csproj", "src/Server/VK.Infrastructure/"]
COPY ["src/Shared/VK.Shared/VK.Shared.csproj", "src/Shared/VK.Shared/"]

RUN dotnet restore "src/Server/VK.Web/VK.Web.csproj"

COPY . .
RUN dotnet publish "src/Server/VK.Web/VK.Web.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_FORWARDEDHEADERS_ENABLED=true

COPY --from=build /app/publish .

EXPOSE 8080
ENTRYPOINT ["sh", "-c", "dotnet VK.Web.dll --urls http://0.0.0.0:${PORT:-8080}"]
