#! /bin/sh
#
# run-simulations.sh
#


echo "Creating logs directory"
mkdir -p logs

echo "Creating logs/results.csv"
echo "k,bytes,time" > logs/results.csv

for size in 16384 32768 65536 131072; do
    for k in 1 8; do
        for i in {1..10}; do
            sleep 0.0625
            ./run.sh ./mesh_network.json --populate-db --simulate-frames 9600 <<EOF
topk A $k
topk P $k
send A P $size --quit-on-transmit
EOF
        done | grep -oE 'in ([0-9]+\.[0-9]+) seconds' | \
            grep -oE '[0-9]+\.[0-9]+' > logs/simulation_size_${size}_${k}.csv
        echo "Finished runs for size=$size, k=$k"

        # combine all logs into a single results file
        sed "s/^/${k},${size},/" logs/simulation_size_${size}_${k}.csv >> logs/results.csv
    done
done
