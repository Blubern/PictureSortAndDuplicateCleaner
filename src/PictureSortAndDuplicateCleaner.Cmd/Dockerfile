FROM mcr.microsoft.com/dotnet/runtime:10.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY src/ ./
RUN dotnet restore "PictureSortAndDuplicateCleaner.slnx"
RUN dotnet publish "PictureSortAndDuplicateCleaner.Cmd/PictureSortAndDuplicateCleaner.Cmd.csproj" \
    -c Release \
    -o /app/publish \
    /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "PictureSortAndDuplicateCleaner.Cmd.dll"]
