machine Journal {
    var subs: map[string, Subscription];

    start state Init {
        on eStart goto Run;
    }

    state Run {
        on eEnqueueRequest do (payload: (key: string, t: float)) {
            var s_id: string;
            foreach (s_id in keys(subs)) {
                if (subs[s_id].key == payload.key) {
                    subs[s_id].queue += (payload.t);
                }
            }
        }
        on eSubscribeRequest do (payload: (s_id: string, key: string)) {
            var subscription: Subscription;
            subscription.t = 0.0;
            subscription.key = payload.key;
            subs[payload.s_id] = subscription;
        }
        on ePollRequest do (payload: (s_id: string, storage: Storage)) {
            var _ePollResponsePayload: (t: float);
            var t: float;
            var tMin: float;
            _ePollResponsePayload.t = -1.0;
            if (sizeof(subs[payload.s_id].queue) > 0) {
                tMin = subs[payload.s_id].queue[0];
                foreach (t in subs[payload.s_id].queue) {
                    if (t < tMin) {
                        tMin = t;
                    }
                }
                subs[payload.s_id].queue -= (tMin);
                _ePollResponsePayload.t = tMin;
            }
            send payload.storage, ePollResponse, _ePollResponsePayload;
        }
    }
}
