FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["src/Server/VK.API/VK.API.csproj", "src/Server/VK.API/"]
COPY ["src/Server/VK.Core/VK.Core.csproj", "src/Server/VK.Core/"]
COPY ["src/Server/VK.Infrastructure/VK.Infrastructure.csproj", "src/Server/VK.Infrastructure/"]
COPY ["src/Shared/VK.Shared/VK.Shared.csproj", "src/Shared/VK.Shared/"]
COPY ["src/Shared/VK.Contracts/VK.Contracts.csproj", "src/Shared/VK.Contracts/"]

RUN dotnet restore "src/Server/VK.API/VK.API.csproj" -v minimal

COPY . .
RUN dotnet publish "src/Server/VK.API/VK.API.csproj" -c Release -o /app/publish /p:UseAppHost=false -v minimal

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

RUN apt-get update \
    && apt-get install -y --no-install-recommends python3 python3-pip \
    && pip3 install --no-cache-dir edge-tts \
    && rm -rf /var/lib/apt/lists/*

ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_FORWARDEDHEADERS_ENABLED=true

COPY --from=build /app/publish .
RUN mkdir -p /app/App_Data/audio

EXPOSE 8080
ENTRYPOINT ["sh", "-c", "dotnet VK.API.dll --urls http://0.0.0.0:${PORT:-8080}"]
