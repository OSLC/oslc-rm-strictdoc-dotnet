FROM mcr.microsoft.com/dotnet/sdk:11.0.100-rc.1-alpine3.24 AS build
ARG TARGETARCH
RUN apk add --no-cache binutils build-base clang openssl-dev zlib-dev
WORKDIR /src
COPY ["OSLC4Net.Server.LogRelay/OSLC4Net.Server.LogRelay.csproj", "OSLC4Net.Server.LogRelay/"]
RUN dotnet restore "OSLC4Net.Server.LogRelay/OSLC4Net.Server.LogRelay.csproj"
COPY ["OSLC4Net.Server.LogRelay/", "OSLC4Net.Server.LogRelay/"]
RUN case "$TARGETARCH" in \
      amd64) rid=linux-musl-x64 ;; \
      arm64) rid=linux-musl-arm64 ;; \
      *) echo "Unsupported TARGETARCH: $TARGETARCH" >&2; exit 64 ;; \
    esac; \
    dotnet publish "OSLC4Net.Server.LogRelay/OSLC4Net.Server.LogRelay.csproj" \
      -c Release -r "$rid" --self-contained true -o /out

FROM alpine:3.24
RUN addgroup -S relay && adduser -S -G relay relay
WORKDIR /app
COPY --from=build /out/ ./
USER relay
EXPOSE 8742
ENV LOG_RELAY_URLS=http://0.0.0.0:8742
ENTRYPOINT ["./OSLC4Net.Server.LogRelay"]
