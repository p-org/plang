machine TestCase {
    start state Init {
        entry {
            var request_qps: int;
            var response_qps: int;
            var maxTime: int;
            var nServerFails: int;
            var serverFailOffset: int;
            var requestDropRate: float;
            var kClientAmplification: int;
            var clientAmplificationOffset: int;
            var nRetries: int;
            var retryRateThreshold: float;
            var server: Server;
            var client: Client;
            var _ServerPayload: (maxQPS: int, nServerFails: int, serverFailOffset: int, requestDropRate: float);
            var _ClientPayload: (maxQPS: int, server: Server, maxTime: int, kClientAmplification: int, clientAmplificationOffset: int, nRetries: int, retryRateThreshold: float);

            request_qps = 10;
            response_qps = 10;
            maxTime = 100;
            nServerFails = 0;
            requestDropRate = 0.4;
            kClientAmplification = 1;
            nRetries = -2;
            retryRateThreshold = 0.5;
            serverFailOffset = 20;
            clientAmplificationOffset = 0;

            _ServerPayload.maxQPS = response_qps;
            _ServerPayload.nServerFails = nServerFails;
            _ServerPayload.serverFailOffset = serverFailOffset;
            _ServerPayload.requestDropRate = requestDropRate;
            server = new Server(_ServerPayload);
            send server, eStart;

            _ClientPayload.maxQPS = request_qps;
            _ClientPayload.server = server;
            _ClientPayload.maxTime = maxTime;
            _ClientPayload.kClientAmplification = kClientAmplification;
            _ClientPayload.clientAmplificationOffset = clientAmplificationOffset;
            _ClientPayload.nRetries = nRetries;
            _ClientPayload.retryRateThreshold = retryRateThreshold;
            client = new Client(_ClientPayload);
            send client, eStart;
        }
    }
}
