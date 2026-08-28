FROM mcr.microsoft.com/dotnet/sdk:10.0.400

WORKDIR /workspace

ENV DOTNET_USE_POLLING_FILE_WATCHER=1 \
    DOTNET_WATCH_SUPPRESS_STATIC_FILE_HANDLING=1 \
    ASPNETCORE_HTTP_PORTS=8080

COPY .editorconfig global.json Directory.Build.props Directory.Packages.props EngageOps.slnx ./
COPY src/backend/EngageOps.Api/EngageOps.Api.csproj src/backend/EngageOps.Api/
COPY tests/backend/EngageOps.Api.Tests/EngageOps.Api.Tests.csproj tests/backend/EngageOps.Api.Tests/

RUN dotnet restore EngageOps.slnx

COPY src/backend/EngageOps.Api/ src/backend/EngageOps.Api/

EXPOSE 8080

CMD ["dotnet", "watch", "--non-interactive", "--project", "src/backend/EngageOps.Api/EngageOps.Api.csproj", "--no-launch-profile", "--no-restore"]
