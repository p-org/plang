echo "avg_t,p99_t,n_requests,n_responses,success_percentage,request_qps,response_qps,max_time,n_server_fails,k_client_amplification,n_retries,retry_rate_threshold,request_drop_rate"
for n_retries in -2; do
	for retry_rate_threshold in 0.5; do
		for n_server_fails in 0; do
			for k_client_amplification in 1; do
				for request_drop_rate in 0.2 0.4 0.6 0.8; do
					for response_qps in 10; do
						for max_time in 100; do
							for request_qps in 10; do
								/usr/bin/python3 modify_test.py $request_qps $response_qps $max_time $n_server_fails $k_client_amplification $n_retries $retry_rate_threshold $request_drop_rate
								dotnet /Users/beyazity/Documents/P/Bld/Drops/Release/Binaries/net6.0/p.dll compile 1> out.log 2> err.log
								dotnet /Users/beyazity/Documents/P/Bld/Drops/Release/Binaries/net6.0/p.dll check --sch-statistical -i 1 -ms 1000000000 1> out.log 2> err.log
								/usr/bin/python3 analyzer.py $request_qps $response_qps $max_time $n_server_fails $k_client_amplification $n_retries $retry_rate_threshold $request_drop_rate 2> err.log
								rm err.log
								rm out.log
							done
						done
					done
				done
			done
		done
	done
done
