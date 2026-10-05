FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY doc-service.csproj ./
RUN --mount=type=cache,id=prdal-doc-nuget,sharing=locked,target=/root/.nuget/packages dotnet restore
COPY . ./
RUN --mount=type=cache,id=prdal-doc-nuget,sharing=locked,target=/root/.nuget/packages dotnet publish -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0
RUN apt-get update && apt-get install -y --no-install-recommends curl libfontconfig1 && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=build /app ./
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "doc-service.dll"]
