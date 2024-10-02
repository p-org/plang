# Draw graphs for results from backpressure simulation
# Run `backpressure.py | tee results.txt` first
library(ggplot2)

dpi=200
width=7
height=5

d=read.table("results.csv", sep=",", header=TRUE)
# d$col=sprintf("n_server_fails=%d", d$n_server_fails)
# d$col=sprintf("k_client_amplification=%d", d$k_client_amplification)
d$col=sprintf("request_drop_rate=%f", d$request_drop_rate)

gg = ggplot(d, aes(x=request_qps, y=success_percentage, color=col)) +
    geom_line() + geom_point() +
    xlab("Requests Per Second") +
    ylab("Success Percentage") +
    coord_cartesian(ylim = c(0,100))

ggsave("req_vs_succ.pdf", width=width, height=height, dpi=dpi)

gg = ggplot(d, aes(x=request_qps, y=avg_t, color=col)) +
    geom_line() + geom_point() +
    xlab("Requests Per Second") +
    ylab("Average Latency") +
    coord_cartesian(ylim = c(0,100))

ggsave("req_vs_lat.pdf", width=width, height=height, dpi=dpi)

# gg = ggplot(d, aes(x=write_n, y=avg_t, color=col)) +
#     geom_line() + geom_point() +
#     xlab("Write Goodput") +
#     ylab("Read Latency (avg)") +
#     coord_cartesian(ylim = c(0,15))

# ggsave("goodput_vs_latency.png", width=width, height=height, dpi=dpi)
# ggsave("goodput_vs_latency.pdf", width=width, height=height, dpi=dpi)
