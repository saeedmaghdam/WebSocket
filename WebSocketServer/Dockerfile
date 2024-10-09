#See https://aka.ms/customizecontainer to learn how to customize your debug container and how Visual Studio uses this Dockerfile to build your images for faster debugging.
ARG BUILD_CONFIGURATION=Release

FROM mcr.microsoft.com/dotnet/sdk:8.0@sha256:35792ea4ad1db051981f62b313f1be3b46b1f45cadbaa3c288cd0d3056eefb83 AS base

FROM base AS restore
WORKDIR /src
COPY ["WebSocketServer/WebSocketServer.csproj", "WebSocketServer/"]
RUN --mount=type=cache,target=/root/.nuget/packages \
    dotnet restore ./WebSocketServer/WebSocketServer.csproj

FROM restore AS build-env    
WORKDIR /src
COPY ./WebSocketServer ./WebSocketServer
RUN ls -la
RUN --mount=type=cache,target=/root/.nuget/packages \
    dotnet build -c $BUILD_CONFIGURATION --no-restore ./WebSocketServer/WebSocketServer.csproj

FROM build-env AS publish
WORKDIR /src
RUN --mount=type=cache,target=/root/.nuget/packages \
    dotnet publish -c $BUILD_CONFIGURATION --no-restore -o /app/publish ./WebSocketServer/WebSocketServer.csproj

FROM alpine:3.20@sha256:beefdbd8a1da6d2915566fde36db9db0b524eb737fc57cd1367effd16dc0d06d
WORKDIR /app
EXPOSE 5000

COPY --from=publish /app/publish .

USER root

# Install all dependencies
RUN apk update && \
    apk upgrade && \
    apk add --no-cache bash icu-libs krb5-libs libgcc libintl libssl3 libstdc++ zlib

# Install .NET runtime
RUN wget https://dot.net/v1/dotnet-install.sh -O dotnet-install.sh && \
    chmod +x dotnet-install.sh && \
    ./dotnet-install.sh --channel 8.0 --version latest --runtime aspnetcore

# # Create a group and user
# RUN addgroup -S app && adduser -S app -G app

# # Give permissions to the app user
# RUN chown -R app:app /app

# # Change to the app user
# USER app

# Set the ASPNETCORE_URLS environment variable
ENV ASPNETCORE_URLS=http://+:5000

# Start the app
ENTRYPOINT [ "/root/.dotnet/dotnet", "WebSocketServer.dll" ]