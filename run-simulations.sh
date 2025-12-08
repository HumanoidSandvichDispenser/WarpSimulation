#! /bin/sh
#
# run-simulations.sh
#


mkdir -p logs

#for size in 16384 32768 65536 131072; do
#for size in 131072; do
for size in 16384 32768 65536 131072; do
    for k in 1 8; do
        for i in {1..50}; do
            sleep 1
            echo "Run $i (size=$size, k=$k)"
                ./run.sh ./example_simulation2.json --populate-db --simulate-frames 9600 <<EOF
topk A $k
topk P $k
send A P $size --quit-on-transmit
EOF
            done | tee -a logs/simulation_size_${size}_${k}.log
        done
    done
done
