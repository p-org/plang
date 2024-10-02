machine Writer {
    var initT: float;
    var every_t: float;
    var journal: Journal;
    var backPressure: bool;
    var storages: map[string, Storage];

    start state Init {
        entry (payload: (initT: float, every_t: float, journal: Journal, storages: map[string, Storage], backPressure: bool)) {
            initT = payload.initT;
            every_t = payload.every_t;
            journal = payload.journal;
            backPressure = payload.backPressure;
            storages = payload.storages;
        }
        on eStart goto Run with {
            var _eWriterRunPayload: (t: float);
            _eWriterRunPayload.t = initT;
            send this, eWriterRun, _eWriterRunPayload, delay format("{0}", initT);
        }
    }

    state Run {
        on eWriterRun do (payload: (t: float)) {
            var _eEnqueueRequestPayload: (key: string, t: float);
            var _eWriterRunPayload: (t: float);
            var _eAtTRequestPayload: (writer: Writer);
            var lag: float;
            var at_t: float;
            var _delay: float;
            _eEnqueueRequestPayload.key = choose(keys(storages));
            _eEnqueueRequestPayload.t = payload.t;
            send journal, eEnqueueRequest, _eEnqueueRequestPayload;
            _delay = Expovariate(1.0 / every_t);
            if (backPressure) {
                _eAtTRequestPayload.writer = this;
                send storages[_eEnqueueRequestPayload.key], eAtTRequest, _eAtTRequestPayload;
                receive {
                    case eAtTResponse: (payload: (t: float)) {
                        at_t = payload.t;
                    }
                }
                lag = payload.t - at_t;
                if (lag > 1.0) {
                    _delay = _delay + lag - 1.0;
                }
            }
            _eWriterRunPayload.t = payload.t + _delay;
            send this, eWriterRun, _eWriterRunPayload, delay format("{0}", _delay); // This should be a delayed event with backpressure delay
        }
    }
}
