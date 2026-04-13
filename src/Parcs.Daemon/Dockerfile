# See https://aka.ms/customizecontainer to learn how to customize your debug container and how Visual Studio uses this Dockerfile to build your images for faster debugging.

# ── Build stage ─────────────────────────────────────────────────────────────────
# Uses the standard .NET SDK image; no CUDA needed at build time because ILGPU
# compiles GPU kernels at runtime (JIT) rather than via nvcc at build time.
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["src/Parcs.Daemon/Parcs.Daemon.csproj", "src/Parcs.Daemon/"]
COPY ["src/Parcs.Core/Parcs.Core.csproj", "src/Parcs.Core/"]
RUN dotnet restore "./src/Parcs.Daemon/Parcs.Daemon.csproj"
COPY . .
WORKDIR "/src/src/Parcs.Daemon"
RUN dotnet build "./Parcs.Daemon.csproj" -c $BUILD_CONFIGURATION -o /app/build

# ── Publish stage ────────────────────────────────────────────────────────────────
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./Parcs.Daemon.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

# ── Runtime stage ────────────────────────────────────────────────────────────────
# Base: NVIDIA CUDA 12.3 runtime on Ubuntu 22.04.
# The "runtime" variant (vs "devel") includes libcuda, libcublas, and the driver
# shim — everything ILGPU needs to JIT-compile and execute CUDA kernels.
# The .NET 8 runtime is installed on top via Microsoft's official package feed.
#
# If the pod is scheduled on a CPU-only node (no nvidia.com/gpu resource requested),
# ILGPU transparently falls back to its CPU accelerator — no code changes needed.
FROM nvidia/cuda:12.3.2-runtime-ubuntu22.04 AS final

# Install .NET 8 runtime from the official Microsoft package repository.
# Using --no-install-recommends keeps the image lean.
RUN apt-get update \
    && apt-get install -y --no-install-recommends wget ca-certificates \
    && wget -q https://packages.microsoft.com/config/ubuntu/22.04/packages-microsoft-prod.deb \
         -O /tmp/packages-microsoft-prod.deb \
    && dpkg -i /tmp/packages-microsoft-prod.deb \
    && rm /tmp/packages-microsoft-prod.deb \
    && apt-get update \
    && apt-get install -y --no-install-recommends dotnet-runtime-8.0 \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Parcs.Daemon.dll"]