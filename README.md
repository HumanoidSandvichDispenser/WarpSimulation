# WARP Simulation

This program simulates the novel Weighted Average Routing Protocol (WARP) for
efficient multipath routing in mesh networks to increase throughput and reduce
loss.

This README is also available on [GitHub](https://github.com/HumanoidSandvichDispenser/WarpSimulation)
for easier viewing.

## Build Instructions

This C# program requires the [.NET SDK](https://dotnet.microsoft.com/en-us/download)
to build and run. The minimum required version is **.NET 9.0**.

Run `dotnet build` in the project root directory (where this file is) to build
the program. This automatically restores any required dependencies.

## Running the Simulation

After building, you can run the simulation using the following command:

```sh
dotnet run --project WarpSimulation/WarpSimulation.csproj -- [arguments ...]
```

Because this command is long, a shell script `run.sh` is provided for
convenience. The script passes all arguments to the `dotnet run` command.
You can run the simulation with the provided mesh network JSON file using
and default parameters as:

```sh
./run.sh mesh_network.json
```

This command starts the simulation with the specified JSON file describing the
network topology. The next section of this README describes the available
commands. For examples on how these commands can be used, Section 3 of the
report `warp-report.pdf` contains various scenarios and their corresponding
command sequences.

### (Optional) Statistical Analysis

There is also a shell script that runs simulations to generate data for the
statistical analysis in the report. After running it, the resulting CSV file
should appear in `logs/results.csv`, and the Python scripts can be used for
statistical analysis and plotting.

This requires Python 3 with the `pandas`, `seaborn`, and `matplotlib` packages
installed.

## Simulation Commands

Running the simulation through the command line spawns a graphical interface
while allowing you to input commands to control the simulation through the
console. Below are commands that can be used during the simulation:

- `send <source name> <destination name> <size> [--quit-on-transmit]`: Sends
  a packet of the specified size from the source node to the destination node,
  optionally quitting the simulation once all data has been transmitted.
- `topk <node name> <k>` - Sets the maximum number of candidate paths, k, to be
  considered by the specified node when routing packets.
- `view [node name]` - Views the node's perceived network topology. If no node
  name is provided, views the actual network topology. This also adds
- `toggle <node name>` - Toggles the specified node between active and inactive
  states.

## Technical Report

A detailed technical report describing the WARP protocol and simulation results
is available in `warp-report.pdf`

# Code Overview (where is what?)

Because this project contains multiple components with each file having
hundreds of lines of code, reading the comments alone may not be sufficient to
understand the program and its structure.

Below is a brief description of the important files and their purposes:

- `Program.cs`: The main entry point of the program. It handles
  command line arguments, initializes the simulation, and starts the command
  loop and graphical interface.
- `Simulation.cs`: The simulation class that manages the overall
  simulation state, including the network graph, nodes, packets, and time steps.
  Commands from the user are also processed here.
- `WarpNode.cs`: The class representing a node in the mesh network.
  This class is responsible for choosing the next hop, encapsulating packets,
  sending and receiving packets (including hellos and LSAs). This also contains
  the algorithm used to **filter out paths** based on similarity.
- `WarpDatabase.cs`: The class representing the routing database for a
  node, maintaining the link state information. This also contains the
  algorithm used to **select paths** based on weight and round-robin deficit.
  `WarpNode` calls into this class whenever it needs to update the database
  with new LSAs or query the database for a path.
- `UndirectedWeightedGraph.cs`: A generic undirected weighted graph
  implementation.
- `UndirectedWeightedGraph.Algorithms.cs`: A partial class
  (extension) for `UndirectedWeightedGraph` that contains various graph
  algorithms, including Dijkstra's shortest path algorithm and Yen's K-Shortest
  Paths algorithm.
- `WarpNetworkGraph.cs`: A subclass of `UndirectedWeightedGraph`
  that represents the mesh network topology.
- `Link.cs`: The class representing a link between two nodes in the
  mesh network.
- `Packets/*.cs`, `TransportLayer/TcpSegment.cs`: Various packet types used in
  the simulation, including data packets, hello packets, and link state
  advertisements (LSAs).
- `TransportLayer/TcpSession.cs`: The class representing a TCP session
  between two nodes, handling segmentation and reassembly of data packets.
- `ByteQueue.cs`: A simple queue for bytes, used by `WarpNode` to buffer
  outgoing data. This is also used by `WarpDatabase` to track the size of the
  queue to determine weights for paths.
