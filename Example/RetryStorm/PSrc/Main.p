fun Random(): float;

event eStart;
event eRequest: (id: int, client: Client, isRetry: bool);
event eResponse: (id: int);
event eServerRun;
event eClientRun;

module Module = { Client, Server };
