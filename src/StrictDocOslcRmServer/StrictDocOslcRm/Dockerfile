FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build
ARG TARGETARCH
WORKDIR /src

COPY ["Directory.Packages.props", "."]
COPY ["OSLC4Net.Server.Diagnostics/OSLC4Net.Server.Diagnostics.csproj", "./OSLC4Net.Server.Diagnostics/"]
COPY ["StrictDocOslcRm/StrictDocOslcRm.csproj", "./StrictDocOslcRm/"]
RUN DOTNET_ARCH="$([ "$TARGETARCH" = "amd64" ] && echo "x64" || echo "$TARGETARCH")" && \
    dotnet restore "StrictDocOslcRm/StrictDocOslcRm.csproj" -r "linux-musl-$DOTNET_ARCH"

COPY . .
WORKDIR "/src/StrictDocOslcRm"
# WARN: trim break the conneg for some reason
RUN DOTNET_ARCH="$([ "$TARGETARCH" = "amd64" ] && echo "x64" || echo "$TARGETARCH")" && \
    dotnet publish "StrictDocOslcRm.csproj" -c Release -o /app/publish --self-contained -r "linux-musl-$DOTNET_ARCH" --no-restore /p:DebugType=None /p:DebugSymbols=false

# -extra adds full support for Unicode and timezones
FROM mcr.microsoft.com/dotnet/runtime-deps:10.0-alpine-extra AS runtime

LABEL org.opencontainers.image.title="OSLC RM server for StrictDoc"
LABEL org.opencontainers.image.description="OSLC server for StrictDoc in the Requirements Management domain"
LABEL org.opencontainers.image.authors="Andrew Berezovskyi"
LABEL org.opencontainers.image.vendor="Andrew Berezovskyi"
LABEL org.opencontainers.image.license="EPL-2.0"

WORKDIR /app
EXPOSE 8080
COPY --from=build /app/publish .
ENTRYPOINT ["./StrictDocOslcRm"]
