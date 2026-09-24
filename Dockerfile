FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ChambaPoint.Api/ChambaPoint.Api.csproj ChambaPoint.Api/
RUN dotnet restore ChambaPoint.Api/ChambaPoint.Api.csproj
COPY ChambaPoint.Api/ ChambaPoint.Api/
COPY frontend/ frontend/
RUN dotnet publish ChambaPoint.Api/ChambaPoint.Api.csproj -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_ENVIRONMENT=Production
EXPOSE 10000
CMD ["sh", "-c", "dotnet ChambaPoint.Api.dll --urls http://0.0.0.0:${PORT:-10000}"]
