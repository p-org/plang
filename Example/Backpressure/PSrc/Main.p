fun Random(): float;
fun Expovariate(lambda: float): float;

type Subscription = (t: float, key: string, queue: set[float]);

event eStart;
event eReaderRun: (t: float);
event eWriterRun: (t: float);
event eStorageRun: (t: float);
event ePollRequest: (s_id: string, storage: Storage);
event ePollResponse: (t: float);
event eEnqueueRequest: (key: string, t: float);
event eSubscribeRequest: (s_id: string, key: string);
event eReadRequest: (t: float, reader: Reader);
event eReadResponse: (tStart: float, tEnd:float);
event eAtTRequest: (writer: Writer);
event eAtTResponse: (t: float);

module Module = { Journal, Storage, Reader, Writer };
