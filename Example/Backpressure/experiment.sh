echo "avg_t,p99_t,read_n,write_n,rho,shards,backpressure"
for shards in 1 5; do
	for rho in 0.02 0.04 0.06 0.08 0.1 0.12 0.14 0.16 0.18 0.2 0.22 0.24 0.26 0.28 0.3 0.32 0.34 0.36 0.38 0.4 0.42 0.44 0.46 0.48 0.5 0.52 0.54 0.56 0.58 0.6 0.62 0.64 0.66 0.68 0.7 0.72 0.74 0.76 0.78 0.8 0.82 0.84 0.86 0.88 0.9 0.92 0.94 0.96 0.98; do
		for back_pressure in 1 0; do
			/usr/bin/python3 modify_test.py $shards $rho $back_pressure
			dotnet /Users/beyazity/Documents/P/Bld/Drops/Release/Binaries/net6.0/p.dll compile 1> out.log 2> err.log
			dotnet /Users/beyazity/Documents/P/Bld/Drops/Release/Binaries/net6.0/p.dll check --sch-statistical -i 1 -ms 1000000000 1> out.log 2> err.log
			/usr/bin/python3 analyzer.py $shards $rho $back_pressure 2> err.log
			rm err.log
			rm out.log
		done
	done
done
