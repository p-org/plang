machine Server {
    var maxQPS: int;
    var currentQPS: int;
    var servedRequestIds: set[int];
    var nServerFails: int;
    var requestDropRate: float;
    var serverFailOffset: int;
    var time: int;
    start state Init {
        entry (payload: (maxQPS: int, nServerFails: int, serverFailOffset: int, requestDropRate: float)) {
            maxQPS = payload.maxQPS;
            nServerFails = payload.nServerFails;
            serverFailOffset = payload.serverFailOffset;
            requestDropRate = payload.requestDropRate;
            currentQPS = 0;
            time = 0;
        }
        on eStart goto Wait with {
            send this, eServerRun, delay "0.2";
        }
    }
    state Wait {
        defer eRequest;
        on eServerRun do {
            time = time + 1;
            currentQPS = 0;
            send this, eServerRun;
            if (time > serverFailOffset && nServerFails > 0) {
                nServerFails = nServerFails - 1;
            } else {
                goto Run;
            }
       }
    }
    state Run {
        defer eServerRun;
        on eRequest do (payload: (id: int, client: Client, isRetry: bool)) {
            var _eResponsePayload: (id: int);
            var p: float;
            p = Random();
            if (p >= requestDropRate) {
                if (!(payload.id in servedRequestIds)) {
                    _eResponsePayload.id = payload.id;
                    send payload.client, eResponse, _eResponsePayload;
                    servedRequestIds += (payload.id);
                }
            }
            currentQPS = currentQPS + 1;
            if (currentQPS == maxQPS) {
                goto Wait;
            }
        }
        on null goto Wait;
    }
}
