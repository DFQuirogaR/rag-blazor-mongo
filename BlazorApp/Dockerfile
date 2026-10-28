FROM mcr.microsoft.com/dotnet/sdk:10.0-preview AS build

WORKDIR /src

COPY ["BlazorApp.csproj", "./"]
RUN dotnet restore "BlazorApp.csproj"

COPY . .
RUN dotnet publish "BlazorApp.csproj" -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0-preview AS final

WORKDIR /app
COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "BlazorApp.dll"]
