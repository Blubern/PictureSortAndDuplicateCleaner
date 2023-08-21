FROM mcr.microsoft.com/dotnet/runtime:7.0 AS base

FROM mcr.microsoft.com/dotnet/sdk:7.0 AS build
WORKDIR /app
COPY . .
WORKDIR /app/src/PictureSort.Cmd
RUN dotnet publish "PictureSort.Cmd.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=build . .

ENTRYPOINT ["dotnet", "PictureSort.Cmd.dll"]