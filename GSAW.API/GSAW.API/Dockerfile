FROM microsoft/dotnet:2.1-aspnetcore-runtime AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM microsoft/dotnet:2.1-sdk AS build
WORKDIR /src
COPY ["GSAW.API/GSAW.API.csproj", "GSAW.API/"]
RUN dotnet restore "GSAW.API/GSAW.API.csproj"
COPY . .
WORKDIR "/src/GSAW.API"
RUN dotnet build "GSAW.API.csproj" -c Release -o /app

FROM build AS publish
RUN dotnet publish "GSAW.API.csproj" -c Release -o /app

FROM base AS final
WORKDIR /app
COPY --from=publish /app .
ENTRYPOINT ["dotnet", "GSAW.API.dll"]