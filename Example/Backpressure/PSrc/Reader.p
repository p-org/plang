machine Reader {
    var initT: float;
    var every_t: float;
    var storages: map[string, Storage];
    var journal: Journal;
    var writer: Writer;
    var n: int;

    start state Init {
        entry (payload: (initT: float, every_t: float, storages: map[string, Storage], journal: Journal, writer: Writer, n: int)) {
            initT = payload.initT;
            every_t = payload.every_t;
            storages = payload.storages;
            journal = payload.journal;
            writer = payload.writer;
            n = payload.n;
        }
        on eStart goto Run with {
            var _eReaderRunPayload: (t: float);
            _eReaderRunPayload.t = initT;
            send this, eReaderRun, _eReaderRunPayload, delay format("{0}", initT);
        }
    }

    state Run {
        on eReaderRun do (payload: (t: float)) {
            var _eReadRequestPayload: (t: float, reader: Reader);
            var _eReaderRunPayload: (t: float);
            _eReadRequestPayload.t = payload.t;
            _eReadRequestPayload.reader = this;
            send storages[choose(keys(storages))], eReadRequest, _eReadRequestPayload;
            _eReaderRunPayload.t = payload.t + every_t;
            send this, eReaderRun, _eReaderRunPayload, delay format("{0}", every_t); // This should be a delayed event
        }
        on eReadResponse do (payload: (tStart: float, tEnd: float)) {
            var tStart: float;
            var tEnd: float;
            var storage: Storage;
            tStart = payload.tStart;
            tEnd = payload.tEnd;
            n = n - 1;
            if (n == 0) {
                foreach (storage in values(storages)) {
                    send storage, halt;
                }
                send journal, halt;
                send writer, halt;
                raise halt;
            }
        }
    }
}
