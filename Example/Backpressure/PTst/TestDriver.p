machine TestCase {
    start state Init {
        entry {
            var journal: Journal;
            var writer: Writer;
            var reader: Reader;
            var storages: map[string, Storage];
            var _StoragePayload: (initT:float, every_t: float, key: string, s_id: string, journal: Journal);
            var _WriterPayload: (initT:float, every_t: float, journal: Journal, storages: map[string, Storage], backPressure: bool);
            var _ReaderPayload: (initT:float, every_t: float, storages: map[string, Storage], journal: Journal, writer: Writer, n: int);
            var _payload: (storages: map[string, Storage]);
            var shards: int;
            var shardsFloat: float;
            var rho: float;
            var backpressure: bool;
            var i: int;

            shards = 5;
            shardsFloat = 5.0;
            rho = 0.98;
            backpressure = false;

            journal = new Journal();
            send journal, eStart;

            i = 0;
            while (i < shards) {
                _StoragePayload.initT = Random();
                _StoragePayload.every_t = 1.0;
                _StoragePayload.key = format("key{0}", i);
                _StoragePayload.s_id = format("s_id{0}", i);
                _StoragePayload.journal = journal;
                storages[_StoragePayload.key] = new Storage(_StoragePayload);
                send storages[_StoragePayload.key], eStart;
                i = i + 1;
            }

            _WriterPayload.initT = Random();
            _WriterPayload.every_t = 1.0/(shardsFloat*rho);
            _WriterPayload.journal = journal;
            _WriterPayload.storages = storages;
            _WriterPayload.backPressure = backpressure;
            writer = new Writer(_WriterPayload);
            send writer, eStart;

            _ReaderPayload.initT = Random();
            _ReaderPayload.every_t = 1.0;
            _ReaderPayload.storages = storages;
            _ReaderPayload.journal = journal;
            _ReaderPayload.writer = writer;
            _ReaderPayload.n = 100;
            reader = new Reader(_ReaderPayload);
            send reader, eStart;
        }
    }
}
