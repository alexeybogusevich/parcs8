# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

COPY ["src/Parcs.Agent.Mcp/Parcs.Agent.Mcp.csproj", "src/Parcs.Agent.Mcp/"]
COPY ["src/Parcs.Agent.Runtime/Parcs.Agent.Runtime.csproj", "src/Parcs.Agent.Runtime/"]
COPY ["modules/Parcs.Modules.AgentRunner/Parcs.Modules.AgentRunner.csproj", "modules/Parcs.Modules.AgentRunner/"]
RUN dotnet restore "src/Parcs.Agent.Mcp/Parcs.Agent.Mcp.csproj"

COPY . .

# Build the AgentRunner module first — its output is bundled into the MCP server image
WORKDIR /src/modules/Parcs.Modules.AgentRunner
RUN dotnet publish "Parcs.Modules.AgentRunner.csproj" -c $BUILD_CONFIGURATION -o /app/agent-runner-module /p:UseAppHost=false

# Build the MCP server
WORKDIR /src/src/Parcs.Agent.Mcp
RUN dotnet publish "Parcs.Agent.Mcp.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

# Copy AgentRunner module DLLs alongside the MCP server so AgentRunnerModuleRegistrar can find them
RUN cp /app/agent-runner-module/*.dll /app/publish/agent-runner-module/ 2>/dev/null || mkdir -p /app/publish/agent-runner-module && cp /app/agent-runner-module/*.dll /app/publish/agent-runner-module/

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

# Create the agent-runner-module subdirectory if the RUN above didn't
RUN mkdir -p agent-runner-module

EXPOSE 8080
ENTRYPOINT ["dotnet", "Parcs.Agent.Mcp.dll"]
