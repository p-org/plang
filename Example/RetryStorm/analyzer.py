import sys
import csv
import glob
import numpy as np
import pandas as pd

request_qps = int(sys.argv[1])
response_qps = int(sys.argv[2])
max_time = int(sys.argv[3])
n_server_fails = int(sys.argv[4])
k_client_amplification = int(sys.argv[5])
n_retries = int(sys.argv[6])
retry_rate_threshold = float(sys.argv[7])
request_drop_rate =  float(sys.argv[8])

# Finds the mean latency of the given event.
def find_mean_event_latency_in_log(log):
    send_operations = log[(log.Operation == "Send")]
    dequeue_operations = log[(log.Operation == "Dequeue")]
    read_req_send_operations_of_event = send_operations[(send_operations.Event == "PImplementation.eRequest")]
    read_res_dequeue_operations_of_event = dequeue_operations[(dequeue_operations.Event == "PImplementation.eResponse")]
    n_requests = len(read_req_send_operations_of_event.index)
    n_responses = len(read_res_dequeue_operations_of_event.index)
    read_req_send_operations_of_event.drop(read_req_send_operations_of_event.tail(len(read_req_send_operations_of_event.index) - len(read_res_dequeue_operations_of_event.index)).index, inplace = True)
    assert len(read_req_send_operations_of_event.index) == len(read_res_dequeue_operations_of_event.index)
    latencies = read_res_dequeue_operations_of_event.Time.to_numpy() - read_req_send_operations_of_event.Time.to_numpy()
    if len(latencies) == 0:
        return float("inf"), float("inf"), n_requests, n_responses
    avg_t = np.mean(latencies)
    p99_t = np.percentile(latencies, 99)
    return avg_t, p99_t, n_requests, n_responses

def find_mean_event_latency(logs):
    results = np.array([find_mean_event_latency_in_log(log) for log in logs])
    avg_t, p99_t, n_requests, n_responses = results.mean(axis=0)
    return avg_t, p99_t, int(n_requests), int(n_responses), n_responses/(request_qps*(max_time - 1) + request_qps*k_client_amplification)*100, request_qps, response_qps, max_time, n_server_fails, k_client_amplification, n_retries, retry_rate_threshold, request_drop_rate

log_files = glob.glob("PTimeLogs/Log*.csv")

logs = [pd.read_csv(f) for f in log_files]

print(",".join(str(i) for i in find_mean_event_latency(logs)))
