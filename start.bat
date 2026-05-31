@echo off
echo Starting CustomResoManager...

start "MCP Server" cmd /k "dotnet run --project CustomResoManager.McpServer\CustomResoManager.McpServer.csproj"

timeout /t 3 /nobreak > nul

start "WPF App" cmd /k "dotnet run --project CustomResoManager.csproj"
