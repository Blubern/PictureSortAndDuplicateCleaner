FROM mcr.microsoft.com/dotnet/runtime:7.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:7.0 AS build
WORKDIR /src
COPY ["PictureSort.Cmd/PictureSort.Cmd.csproj", "PictureSort.Cmd/"]
RUN dotnet restore "PictureSort.Cmd/PictureSort.Cmd.csproj"
COPY . .
WORKDIR "/src/PictureSort.Cmd"
RUN dotnet build "PictureSort.Cmd.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "PictureSort.Cmd.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENV PICTURE_SOURCE "/volume1/photo/TobiasBackup"
ENV PICTURE_TARGET "/volume1/photo/Tobias"
ENV LOGGING_TARGET = "/volume1/photo/pictureSortLog.txt"
ENTRYPOINT ["dotnet", "PictureSort.Cmd.dll"]
