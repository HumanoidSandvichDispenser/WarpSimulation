#! /bin/sh
#


#dotnet run --project WarpSimulation/WarpSimulation.csproj "$@" < /dev/stdin
dotnet ./WarpSimulation/bin/Debug/net9.0/WarpSimulation.dll "$@" < /dev/stdin
