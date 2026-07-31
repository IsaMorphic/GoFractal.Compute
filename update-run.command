#!/bin/zsh

# Pull latest sources
git pull
git submodule update --init --recursive

# Run application
dotnet run --project GoFractal.Compute/GoFractal.Compute.csproj --configuration Release -- "$@"