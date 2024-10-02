machine Client {
    var maxQPS: int;
    var server: Server;
    var time: int;
    var maxTime: int;
    var kClientAmplification: int;
    var clientAmplificationOffset: int;
    var nRetries: int;
    var retryRateThreshold: float;
    var currentRequestId: int;
    var waitingIds: set[int];
    var retryCounts: map[int, int];
    var nFail: float;
    var nTotal: float;
    start state Init {
        entry (payload: (maxQPS: int, server: Server, maxTime: int, kClientAmplification: int, clientAmplificationOffset: int, nRetries: int, retryRateThreshold: float)) {
            maxQPS = payload.maxQPS;
            server = payload.server;
            maxTime = payload.maxTime;
            kClientAmplification = payload.kClientAmplification;
            nRetries = payload.nRetries;
            retryRateThreshold = payload.retryRateThreshold;
            clientAmplificationOffset = payload.clientAmplificationOffset;
            currentRequestId = 0;
            time = 0;
            nFail = 0.0;
            nTotal = 0.0;
            assert nRetries != 0 || retryRateThreshold == 0.0; // nRetries == 0 --> retryRateThreshold == 0.0
            assert nRetries != -1 || retryRateThreshold == 1.0; // nRetries == -1 --> retryRateThreshold == 1.0
            assert nRetries != -2 || (retryRateThreshold > 0.0 && retryRateThreshold < 1.0); // nRetries == -2 --> (retryRateThreshold > 0.0 && retryRateThreshold < 1.0)
        }
        on eStart goto Run with {
            send this, eClientRun, delay "0.1";
        }
    }
    state Run {
        on eClientRun do {
            var i: int;
            var _eRequestPayload: (id: int, client: Client, isRetry: bool);
            var nRequests: int;
            if (time == maxTime + 1) {
                send server, halt;
                raise halt;
            }
            if (time < maxTime) {
                i = 0;
                while (i < sizeof(waitingIds)) {
                    if (nRetries == -1 || retryCounts[waitingIds[i]] < nRetries || (nRetries == -2 && (nFail/nTotal) < retryRateThreshold)) {
                        nFail = nFail + 1.0;
                        nTotal = nTotal + 1.0;
                        _eRequestPayload.id = waitingIds[i];
                        _eRequestPayload.client = this;
                        _eRequestPayload.isRetry = true;
                        send server, eRequest, _eRequestPayload;
                        retryCounts[waitingIds[i]] = retryCounts[waitingIds[i]] + 1;
                    }
                    i = i + 1;
                }
                i = 0;
                if (time == clientAmplificationOffset) {
                    nRequests = maxQPS * kClientAmplification;
                }
                else {
                    nRequests = maxQPS;
                }
                while (i < nRequests) {
                    _eRequestPayload.id = currentRequestId;
                    _eRequestPayload.client = this;
                    _eRequestPayload.isRetry = false;
                    send server, eRequest, _eRequestPayload;
                    waitingIds += (currentRequestId);
                    retryCounts[currentRequestId] = 0;
                    currentRequestId = currentRequestId + 1;
                    nTotal = nTotal + 1.0;
                    i = i + 1;
                }
            }
            send this, eClientRun;
            time = time + 1;
        }
        on eResponse do (payload: (id: int)) {
            waitingIds -= (payload.id);
        }
    }
}
