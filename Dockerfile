# 階段 1：使用 .NET 9 SDK 進行建置
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# 複製專案檔並還原 NuGet 相依套件
COPY ["WebAPICore.Api/WebAPICore.Api.csproj", "WebAPICore.Api/"]
RUN dotnet restore "WebAPICore.Api/WebAPICore.Api.csproj"

# 複製全數原始碼並進行 Release 發佈
COPY . .
WORKDIR "/src/WebAPICore.Api"
RUN dotnet publish "WebAPICore.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

# 階段 2：使用超輕量 ASP.NET Core 9 Runtime 進行容器運行
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

# 配置連接埠 (Render 雲端平台預設支援)
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "WebAPICore.Api.dll"]
