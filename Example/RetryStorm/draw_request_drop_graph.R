# Draw graphs for results from backpressure simulation
# Run `backpressure.py | tee results.txt` first
library(ggplot2)

dpi=200
width=7
height=5

d=read.table("temp.csv", sep=",", header=TRUE)
# d$col=sprintf("n_server_fails=%d", d$n_server_fails)
# d$col=sprintf("k_client_amplification=%d", d$k_client_amplification)
d$col=sprintf("%d (-2: 0.5 breaker, -1: inf retry, 0: no retry, 4: 4 retry)", d$n_retries)

gg = ggplot(d, aes(x=request_drop_rate, y=success_percentage, color=col)) +
    geom_line() + geom_point() +
    xlab("Requests Drop Rate") +
    ylab("Success Percentage") +
    coord_cartesian(ylim = c(0,100))

ggsave("req_drop_rate_vs_succ.pdf", width=width, height=height, dpi=dpi)

gg = ggplot(d, aes(x=request_drop_rate, y=n_requests, color=col)) +
    geom_line() + geom_point() +
    xlab("Requests Drop Rate") +
    ylab("Request Load") +
    coord_cartesian(ylim = c(0,10000))

ggsave("req_drop_rate_vs_req_load.pdf", width=width, height=height, dpi=dpi)
