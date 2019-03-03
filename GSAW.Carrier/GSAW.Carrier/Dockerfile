FROM microsoft/dotnet:2.1-runtime AS base
WORKDIR /app

FROM microsoft/dotnet:2.1-sdk AS build
WORKDIR /src
COPY GSAW.Carrier/GSAW.Carrier.csproj GSAW.Carrier/
RUN dotnet restore GSAW.Carrier/GSAW.Carrier.csproj
COPY . .
WORKDIR /src/GSAW.Carrier
RUN dotnet build GSAW.Carrier.csproj -c Release -o /app

FROM build AS publish
RUN dotnet publish GSAW.Carrier.csproj -c Release -o /app

FROM base AS final
WORKDIR /app
COPY --from=publish /app .
ENTRYPOINT ["dotnet", "GSAW.Carrier.dll"]
