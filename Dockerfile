FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ["TravelBlog.Web/TravelBlog.Web.csproj", "TravelBlog.Web/"]
RUN dotnet restore "TravelBlog.Web/TravelBlog.Web.csproj"

COPY . .
WORKDIR "/src/TravelBlog.Web"

RUN dotnet publish "TravelBlog.Web.csproj" --configuration Release --output /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

COPY --from=build /app/publish .

ENV ASPNETCORE_HTTP_PORTS=8080
ENV ASPNETCORE_ENVIRONMENT=Production

EXPOSE 8080

USER app

ENTRYPOINT ["dotnet", "TravelBlog.Web.dll"]