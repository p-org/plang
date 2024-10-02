import sys
import csv
import glob
import numpy as np
import pandas as pd

shards = int(sys.argv[1])
rho = float(sys.argv[2])
back_pressure = int(sys.argv[3])

# Finds the mean latency of the given event.
def find_mean_event_latency_in_log(log):
    send_operations = log[(log.Operation == "Send")]
    dequeue_operations = log[(log.Operation == "Dequeue")]
    read_req_send_operations_of_event = send_operations[(send_operations.Event == "PImplementation.eReadRequest")]
    read_res_dequeue_operations_of_event = dequeue_operations[(dequeue_operations.Event == "PImplementation.eReadResponse")]
    read_req_send_operations_of_event.drop(read_req_send_operations_of_event.tail(len(read_req_send_operations_of_event.index) - len(read_res_dequeue_operations_of_event.index)).index, inplace = True)
    assert len(read_req_send_operations_of_event.index) == len(read_res_dequeue_operations_of_event.index)
    latencies = read_res_dequeue_operations_of_event.Time.to_numpy() - read_req_send_operations_of_event.Time.to_numpy()
    avg_t = np.mean(latencies)
    p99_t = np.percentile(latencies, 99)
    read_n = latencies.shape[0]
    write_res_dequeue_operations_of_event = dequeue_operations[(dequeue_operations.Event == "PImplementation.eWriterRun")]
    write_n = len(write_res_dequeue_operations_of_event.index)
    return avg_t, p99_t, read_n, write_n

def find_mean_event_latency(logs):
    results = np.array([find_mean_event_latency_in_log(log) for log in logs])
    avg_t, p99_t, read_n, write_n = results.mean(axis=0)
    return avg_t, p99_t, int(read_n), int(write_n), rho, shards, back_pressure

log_files = glob.glob("PTimeLogs/Log*.csv")

logs = [pd.read_csv(f) for f in log_files]

print(",".join(str(i) for i in find_mean_event_latency(logs)))
