# WARP Simulation

This program simulates the novel Weighted Average Routing Protocol (WARP) for
efficient multipath routing in mesh networks to increase throughput and reduce
loss.

## Build Instructions

This C# program requires the [.NET SDK](https://dotnet.microsoft.com/en-us/download)
to build and run. The minimum required version is **.NET 9.0**.

Run `dotnet build` in the project directory to build the program.

## Running the Simulation

After building, you can run the simulation using the following command:

```sh
dotnet run --project WarpSimulation/WarpSimulation.csproj -- [arguments ...]
```

Because this command is long, a shell script `run.sh` is provided for
convenience. The script passes all arguments to the `dotnet run` command.
You can run the simulation using:

```sh
./run.sh [arguments ...]
```

## Example Simulation Usage

## Simulation Commands

Running the simulation through the command line spawns a graphical interface
while allowing you to input commands to control the simulation through the
console. Below are commands that can be used during the simulation:

- `send <source name> <destination name> <size>`: Sends a packet of the
  specified size from the source node to the destination node.
- `topk <node name> <k>` - Sets the maximum number of candidate paths, k, to be
  considered by the specified node when routing packets.
- `dumpdb [node name]` - Dumps the routing database of the specified node to
  standard output. If no node name is provided, dumps the databases of all
  nodes.

## Technical Report

A detailed technical report describing the WARP protocol and simulation results
is available in `warp-report.pdf`g
