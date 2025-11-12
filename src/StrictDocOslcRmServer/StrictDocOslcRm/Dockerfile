FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build
WORKDIR /src

COPY ["StrictDocOslcRm/StrictDocOslcRm.csproj", "StrictDocOslcRm/"]
RUN dotnet restore "StrictDocOslcRm/StrictDocOslcRm.csproj"

COPY . .
WORKDIR "/src/StrictDocOslcRm"
# WARN: trim break the conneg for some reason
RUN dotnet publish "StrictDocOslcRm.csproj" -c Release -o /app/publish --self-contained -r linux-musl-x64 /p:DebugType=None /p:DebugSymbols=false

# -extra adds full support for Unicode and timezones
FROM mcr.microsoft.com/dotnet/runtime-deps:9.0-alpine-extra AS runtime

LABEL org.opencontainers.image.title="OSLC RM server for StrictDoc"
LABEL org.opencontainers.image.description="OSLC server for StrictDoc in the Requirements Management domain"
LABEL org.opencontainers.image.authors="Andrew Berezovskyi"
LABEL org.opencontainers.image.vendor="Andrew Berezovskyi"
LABEL org.opencontainers.image.license="EPL-2.0"

WORKDIR /app
EXPOSE 8080
COPY --from=build /app/publish .
ENTRYPOINT ["./StrictDocOslcRm"]
