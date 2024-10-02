# Draw graphs for results from backpressure simulation
# Run `backpressure.py | tee results.txt` first
library(ggplot2)

dpi=200
width=7
height=5

d=read.table("results.csv", sep=",", header=TRUE)
d$col=sprintf("shards=%d bp=%d", d$shards, d$backpressure)

gg = ggplot(d, aes(x=100*rho, y=avg_t, color=col)) +
    geom_line() + geom_point() +
    xlab("Write Load (% of Maximum)") +
    ylab("Read Latency (avg)") +
    coord_cartesian(ylim = c(0,15))

ggsave("load_vs_latency.png", width=width, height=height, dpi=dpi)
ggsave("load_vs_latency.pdf", width=width, height=height, dpi=dpi)

gg = ggplot(d, aes(x=write_n, y=avg_t, color=col)) +
    geom_line() + geom_point() +
    xlab("Write Goodput") +
    ylab("Read Latency (avg)") +
    coord_cartesian(ylim = c(0,15))

ggsave("goodput_vs_latency.png", width=width, height=height, dpi=dpi)
ggsave("goodput_vs_latency.pdf", width=width, height=height, dpi=dpi)
