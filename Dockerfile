FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy project file and restore
COPY MininRouter.csproj .
RUN dotnet restore -r linux-x64

# Install native AOT prerequisites early to cache them
RUN apt-get update && apt-get install -y clang zlib1g-dev libsqlite3-dev

# Copy everything else and build
COPY . .

# Publish Native AOT binary
RUN dotnet publish MininRouter.csproj -c Release -r linux-x64 -p:PublishAot=true -p:StripSymbols=true --self-contained true -o /app/publish
RUN rm -f /app/publish/*.dbg /app/publish/*.pdb

# Final runtime image
FROM debian:bookworm-slim
RUN apt-get update && apt-get install -y libssl3 ca-certificates && rm -rf /var/lib/apt/lists/*
WORKDIR /app

# Server GC off to minimize memory usage for small VPS
ENV DOTNET_gcServer=0
ENV DOTNET_gcConcurrent=0
ENV ASPNETCORE_URLS=http://+:8080

# Copy binary and static assets
COPY --from=build /app/publish/ .
COPY --from=build /src/wwwroot ./wwwroot

EXPOSE 8080
ENTRYPOINT ["./MininRouter"]
