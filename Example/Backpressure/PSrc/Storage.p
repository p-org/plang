machine Storage {
    var initT: float;
    var every_t: float;
    var journal: Journal;
    var s_id: string;
    var at_t: float;
    var blockedReads: set[(t: float, reader: Reader)];
    var eStorageRunT: float;
    var isPollRequestSent: bool;

    start state Init {
        entry (payload: (initT: float, every_t: float, key: string, s_id: string, journal: Journal)) {
            var _eSubscribeRequestPayload: (s_id: string, key: string);
            initT = payload.initT;
            every_t = payload.every_t;
            journal = payload.journal;
            s_id = payload.s_id;
            at_t = 0.0;
            isPollRequestSent = false;
            _eSubscribeRequestPayload.s_id = s_id;
            _eSubscribeRequestPayload.key = payload.key;
            send journal, eSubscribeRequest, _eSubscribeRequestPayload;
        }
        on eStart goto Run with {
            var _eStorageRunPayload: (t: float);
            _eStorageRunPayload.t = initT;
            send this, eStorageRun, _eStorageRunPayload, delay format("{0}", initT);
        }
    }

    state Run {
        on eStorageRun do (payload: (t: float)) {
            var _ePollRequestPayload: (s_id: string, storage: Storage);
            var _eStorageRunPayload: (t: float);
            _ePollRequestPayload.s_id = s_id;
            _ePollRequestPayload.storage = this;
            if (!isPollRequestSent) {
                eStorageRunT = payload.t;
                isPollRequestSent = true;
                send journal, ePollRequest, _ePollRequestPayload;
            }
            _eStorageRunPayload.t = payload.t + every_t;
            send this, eStorageRun, _eStorageRunPayload, delay format("{0}", every_t);
        }
        on eReadRequest do (payload: (t: float, reader: Reader)) {
            blockedReads += (payload);
        }
        on ePollResponse do (payload: (t: float)) {
            var next_t: float;
            var readRequestPayload: (t: float, reader: Reader);
            var _eReadResponsePayload: (tStart: float, tEnd:float);
            var stillBlockedReads: set[(t: float, reader: Reader)];
            isPollRequestSent = false;
            next_t = payload.t;
            if (next_t != -1.0) {
                at_t = next_t;
            } else {
                at_t = eStorageRunT;
            }
            foreach (readRequestPayload in blockedReads) {
                if (readRequestPayload.t > at_t) {
                    stillBlockedReads += (readRequestPayload);
                } else {
                    _eReadResponsePayload.tStart = readRequestPayload.t;
                    _eReadResponsePayload.tEnd = eStorageRunT;
                    send readRequestPayload.reader, eReadResponse, _eReadResponsePayload;
                }
            }
            blockedReads = stillBlockedReads;
        }
        on eAtTRequest do (payload: (writer: Writer)) {
            var _eAtTResponsePayload: (t: float);
            _eAtTResponsePayload.t = at_t;
            send payload.writer, eAtTResponse, _eAtTResponsePayload;
        }
    }
}
