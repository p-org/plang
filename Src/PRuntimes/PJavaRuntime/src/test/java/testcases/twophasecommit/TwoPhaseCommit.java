package testcases.twophasecommit;

/***************************************************************************
 * This file was auto-generated on Wednesday, 22 June 2022 at 11:28:16.
 * Please do not edit manually!
 **************************************************************************/

import prt.events.PEvent;
import prt.exceptions.TransitionException;

import java.text.MessageFormat;
import java.util.*;


public class TwoPhaseCommit {
    /* Enums */
    public static class tTransStatus {
        public static final int SUCCESS = 0;
        public static final int ERROR = 1;
        public static final int TIMEOUT = 2;
    }

    /* Tuples */
    // (key:string,val:int,transId:int)
    public static class PTuple_key_val_trans implements prt.values.PValue<PTuple_key_val_trans> {
        public String key;
        public int val;
        public int transId;

        public PTuple_key_val_trans() {
            this.key = "";
            this.val = 0;
            this.transId = 0;
        }

        public PTuple_key_val_trans(String key, int val, int transId) {
            this.key = key;
            this.val = val;
            this.transId = transId;
        }

        public PTuple_key_val_trans deepClone() {
            return new PTuple_key_val_trans(key, val, transId);
        } // deepClone()

        public boolean equals(Object other) {
            return (this.getClass() == other.getClass() &&
                    this.deepEquals((PTuple_key_val_trans)other)
            );
        } // equals()

        public boolean deepEquals(PTuple_key_val_trans other) {
            return (true
                    && prt.values.Equality.deepEquals(this.key, other.key)
                    && this.val == other.val
                    && this.transId == other.transId
            );
        } // deepEquals()

        public String toString() {
            StringBuilder sb = new StringBuilder("PTuple_key_val_trans");
            sb.append("[");
            sb.append("key=" + key);
            sb.append(",val=" + val);
            sb.append(",transId=" + transId);
            sb.append("]");
            return sb.toString();
        } // toString()
    } //PTuple_key_val_trans class definition

    // (client:Client,trans:(key:string,val:int,transId:int))
    public static class PTuple_clnt_trans implements prt.values.PValue<PTuple_clnt_trans> {
        public long client;
        public PTuple_key_val_trans trans;

        public PTuple_clnt_trans() {
            this.client = 0L;
            this.trans = new PTuple_key_val_trans();
        }

        public PTuple_clnt_trans(long client, PTuple_key_val_trans trans) {
            this.client = client;
            this.trans = trans;
        }

        public PTuple_clnt_trans deepClone() {
            return new PTuple_clnt_trans(client, trans.deepClone());
        } // deepClone()

        public boolean equals(Object other) {
            return (this.getClass() == other.getClass() &&
                    this.deepEquals((PTuple_clnt_trans)other)
            );
        } // equals()

        public boolean deepEquals(PTuple_clnt_trans other) {
            return (true
                    && this.client == other.client
                    && prt.values.Equality.deepEquals(this.trans, other.trans)
            );
        } // deepEquals()

        public String toString() {
            StringBuilder sb = new StringBuilder("PTuple_clnt_trans");
            sb.append("[");
            sb.append("client=" + client);
            sb.append(",trans=" + trans);
            sb.append("]");
            return sb.toString();
        } // toString()
    } //PTuple_clnt_trans class definition

    // (transId:int,status:tTransStatus)
    public static class PTuple_trans_stts implements prt.values.PValue<PTuple_trans_stts> {
        public int transId;
        public int status;

        public PTuple_trans_stts() {
            this.transId = 0;
            this.status = 0;
        }

        public PTuple_trans_stts(int transId, int status) {
            this.transId = transId;
            this.status = status;
        }

        public PTuple_trans_stts deepClone() {
            return new PTuple_trans_stts(transId, status);
        } // deepClone()

        public boolean equals(Object other) {
            return (this.getClass() == other.getClass() &&
                    this.deepEquals((PTuple_trans_stts)other)
            );
        } // equals()

        public boolean deepEquals(PTuple_trans_stts other) {
            return (true
                    && this.transId == other.transId
                    && this.status == other.status
            );
        } // deepEquals()

        public String toString() {
            StringBuilder sb = new StringBuilder("PTuple_trans_stts");
            sb.append("[");
            sb.append("transId=" + transId);
            sb.append(",status=" + status);
            sb.append("]");
            return sb.toString();
        } // toString()
    } //PTuple_trans_stts class definition

    // (client:Client,key:string)
    public static class PTuple_clnt_key implements prt.values.PValue<PTuple_clnt_key> {
        public long client;
        public String key;

        public PTuple_clnt_key() {
            this.client = 0L;
            this.key = "";
        }

        public PTuple_clnt_key(long client, String key) {
            this.client = client;
            this.key = key;
        }

        public PTuple_clnt_key deepClone() {
            return new PTuple_clnt_key(client, key);
        } // deepClone()

        public boolean equals(Object other) {
            return (this.getClass() == other.getClass() &&
                    this.deepEquals((PTuple_clnt_key)other)
            );
        } // equals()

        public boolean deepEquals(PTuple_clnt_key other) {
            return (true
                    && this.client == other.client
                    && prt.values.Equality.deepEquals(this.key, other.key)
            );
        } // deepEquals()

        public String toString() {
            StringBuilder sb = new StringBuilder("PTuple_clnt_key");
            sb.append("[");
            sb.append("client=" + client);
            sb.append(",key=" + key);
            sb.append("]");
            return sb.toString();
        } // toString()
    } //PTuple_clnt_key class definition

    // (key:string,val:int,status:tTransStatus)
    public static class PTuple_key_val_stts implements prt.values.PValue<PTuple_key_val_stts> {
        public String key;
        public int val;
        public int status;

        public PTuple_key_val_stts() {
            this.key = "";
            this.val = 0;
            this.status = 0;
        }

        public PTuple_key_val_stts(String key, int val, int status) {
            this.key = key;
            this.val = val;
            this.status = status;
        }

        public PTuple_key_val_stts deepClone() {
            return new PTuple_key_val_stts(key, val, status);
        } // deepClone()

        public boolean equals(Object other) {
            return (this.getClass() == other.getClass() &&
                    this.deepEquals((PTuple_key_val_stts)other)
            );
        } // equals()

        public boolean deepEquals(PTuple_key_val_stts other) {
            return (true
                    && prt.values.Equality.deepEquals(this.key, other.key)
                    && this.val == other.val
                    && this.status == other.status
            );
        } // deepEquals()

        public String toString() {
            StringBuilder sb = new StringBuilder("PTuple_key_val_stts");
            sb.append("[");
            sb.append("key=" + key);
            sb.append(",val=" + val);
            sb.append(",status=" + status);
            sb.append("]");
            return sb.toString();
        } // toString()
    } //PTuple_key_val_stts class definition

    // (participant:Participant,transId:int,status:tTransStatus)
    public static class PTuple_prtcp_trans_stts implements prt.values.PValue<PTuple_prtcp_trans_stts> {
        public long participant;
        public int transId;
        public int status;

        public PTuple_prtcp_trans_stts() {
            this.participant = 0L;
            this.transId = 0;
            this.status = 0;
        }

        public PTuple_prtcp_trans_stts(long participant, int transId, int status) {
            this.participant = participant;
            this.transId = transId;
            this.status = status;
        }

        public PTuple_prtcp_trans_stts deepClone() {
            return new PTuple_prtcp_trans_stts(participant, transId, status);
        } // deepClone()

        public boolean equals(Object other) {
            return (this.getClass() == other.getClass() &&
                    this.deepEquals((PTuple_prtcp_trans_stts)other)
            );
        } // equals()

        public boolean deepEquals(PTuple_prtcp_trans_stts other) {
            return (true
                    && this.participant == other.participant
                    && this.transId == other.transId
                    && this.status == other.status
            );
        } // deepEquals()

        public String toString() {
            StringBuilder sb = new StringBuilder("PTuple_prtcp_trans_stts");
            sb.append("[");
            sb.append("participant=" + participant);
            sb.append(",transId=" + transId);
            sb.append(",status=" + status);
            sb.append("]");
            return sb.toString();
        } // toString()
    } //PTuple_prtcp_trans_stts class definition

    // (numClients:int,numParticipants:int,numTransPerClient:int,failParticipants:int)
    public static class PTuple_nmcln_nmprt_tpc_flprt implements prt.values.PValue<PTuple_nmcln_nmprt_tpc_flprt> {
        public int numClients;
        public int numParticipants;
        public int numTransPerClient;
        public int failParticipants;

        public PTuple_nmcln_nmprt_tpc_flprt() {
            this.numClients = 0;
            this.numParticipants = 0;
            this.numTransPerClient = 0;
            this.failParticipants = 0;
        }

        public PTuple_nmcln_nmprt_tpc_flprt(int numClients, int numParticipants, int numTransPerClient, int failParticipants) {
            this.numClients = numClients;
            this.numParticipants = numParticipants;
            this.numTransPerClient = numTransPerClient;
            this.failParticipants = failParticipants;
        }

        public PTuple_nmcln_nmprt_tpc_flprt deepClone() {
            return new PTuple_nmcln_nmprt_tpc_flprt(numClients, numParticipants, numTransPerClient, failParticipants);
        } // deepClone()

        public boolean equals(Object other) {
            return (this.getClass() == other.getClass() &&
                    this.deepEquals((PTuple_nmcln_nmprt_tpc_flprt)other)
            );
        } // equals()

        public boolean deepEquals(PTuple_nmcln_nmprt_tpc_flprt other) {
            return (true
                    && this.numClients == other.numClients
                    && this.numParticipants == other.numParticipants
                    && this.numTransPerClient == other.numTransPerClient
                    && this.failParticipants == other.failParticipants
            );
        } // deepEquals()

        public String toString() {
            StringBuilder sb = new StringBuilder("PTuple_nmcln_nmprt_tpc_flprt");
            sb.append("[");
            sb.append("numClients=" + numClients);
            sb.append(",numParticipants=" + numParticipants);
            sb.append(",numTransPerClient=" + numTransPerClient);
            sb.append(",failParticipants=" + failParticipants);
            sb.append("]");
            return sb.toString();
        } // toString()
    } //PTuple_nmcln_nmprt_tpc_flprt class definition

    // (nodes:set[machine],nFailures:int)
    public static class PTuple_nodes_nflrs implements prt.values.PValue<PTuple_nodes_nflrs> {
        public LinkedHashSet<Long> nodes;
        public int nFailures;

        public PTuple_nodes_nflrs() {
            this.nodes = new LinkedHashSet<Long>();
            this.nFailures = 0;
        }

        public PTuple_nodes_nflrs(LinkedHashSet<Long> nodes, int nFailures) {
            this.nodes = nodes;
            this.nFailures = nFailures;
        }

        public PTuple_nodes_nflrs deepClone() {
            return new PTuple_nodes_nflrs((LinkedHashSet<Long>)prt.values.Clone.deepClone(nodes), nFailures);
        } // deepClone()

        public boolean equals(Object other) {
            return (this.getClass() == other.getClass() &&
                    this.deepEquals((PTuple_nodes_nflrs)other)
            );
        } // equals()

        public boolean deepEquals(PTuple_nodes_nflrs other) {
            return (true
                    && prt.values.Equality.deepEquals(this.nodes, other.nodes)
                    && this.nFailures == other.nFailures
            );
        } // deepEquals()

        public String toString() {
            StringBuilder sb = new StringBuilder("PTuple_nodes_nflrs");
            sb.append("[");
            sb.append("nodes=" + nodes);
            sb.append(",nFailures=" + nFailures);
            sb.append("]");
            return sb.toString();
        } // toString()
    } //PTuple_nodes_nflrs class definition


    /* Events */
    public static class DefaultEvent extends prt.events.PEvent<Void> {
        public DefaultEvent() { }
        private Void payload;
        public Void getPayload() { return payload; }

        @Override
        public String toString() {
            return "DefaultEvent";
        } // toString()

    } // PEvent definition for DefaultEvent
    public static class PHalt extends prt.events.PEvent<Void> {
        public PHalt() { }
        private Void payload;
        public Void getPayload() { return payload; }

        @Override
        public String toString() {
            return "PHalt";
        } // toString()

    } // PEvent definition for PHalt
    public static class eWriteTransReq extends prt.events.PEvent<PTuple_clnt_trans> {
        public eWriteTransReq(PTuple_clnt_trans p) { this.payload = p; }
        private PTuple_clnt_trans payload;
        public PTuple_clnt_trans getPayload() { return payload; }

        @Override
        public String toString() {
            return "eWriteTransReq[" + payload + "]";
        } // toString()

    } // PEvent definition for eWriteTransReq
    public static class eWriteTransResp extends prt.events.PEvent<PTuple_trans_stts> {
        public eWriteTransResp(PTuple_trans_stts p) { this.payload = p; }
        private PTuple_trans_stts payload;
        public PTuple_trans_stts getPayload() { return payload; }

        @Override
        public String toString() {
            return "eWriteTransResp[" + payload + "]";
        } // toString()

    } // PEvent definition for eWriteTransResp
    public static class eReadTransReq extends prt.events.PEvent<PTuple_clnt_key> {
        public eReadTransReq(PTuple_clnt_key p) { this.payload = p; }
        private PTuple_clnt_key payload;
        public PTuple_clnt_key getPayload() { return payload; }

        @Override
        public String toString() {
            return "eReadTransReq[" + payload + "]";
        } // toString()

    } // PEvent definition for eReadTransReq
    public static class eReadTransResp extends prt.events.PEvent<PTuple_key_val_stts> {
        public eReadTransResp(PTuple_key_val_stts p) { this.payload = p; }
        private PTuple_key_val_stts payload;
        public PTuple_key_val_stts getPayload() { return payload; }

        @Override
        public String toString() {
            return "eReadTransResp[" + payload + "]";
        } // toString()

    } // PEvent definition for eReadTransResp
    public static class ePrepareReq extends prt.events.PEvent<PTuple_key_val_trans> {
        public ePrepareReq(PTuple_key_val_trans p) { this.payload = p; }
        private PTuple_key_val_trans payload;
        public PTuple_key_val_trans getPayload() { return payload; }

        @Override
        public String toString() {
            return "ePrepareReq[" + payload + "]";
        } // toString()

    } // PEvent definition for ePrepareReq
    public static class ePrepareResp extends prt.events.PEvent<PTuple_prtcp_trans_stts> {
        public ePrepareResp(PTuple_prtcp_trans_stts p) { this.payload = p; }
        private PTuple_prtcp_trans_stts payload;
        public PTuple_prtcp_trans_stts getPayload() { return payload; }

        @Override
        public String toString() {
            return "ePrepareResp[" + payload + "]";
        } // toString()

    } // PEvent definition for ePrepareResp
    public static class eCommitTrans extends prt.events.PEvent<Integer> {
        public eCommitTrans(int p) { this.payload = p; }
        private Integer payload;
        public Integer getPayload() { return payload; }

        @Override
        public String toString() {
            return "eCommitTrans[" + payload + "]";
        } // toString()

    } // PEvent definition for eCommitTrans
    public static class eAbortTrans extends prt.events.PEvent<Integer> {
        public eAbortTrans(int p) { this.payload = p; }
        private Integer payload;
        public Integer getPayload() { return payload; }

        @Override
        public String toString() {
            return "eAbortTrans[" + payload + "]";
        } // toString()

    } // PEvent definition for eAbortTrans
    public static class eInformCoordinator extends prt.events.PEvent<Long> {
        public eInformCoordinator(long p) { this.payload = p; }
        private Long payload;
        public Long getPayload() { return payload; }

        @Override
        public String toString() {
            return "eInformCoordinator[" + payload + "]";
        } // toString()

    } // PEvent definition for eInformCoordinator
    public static class eMonitor_AtomicityInitialize extends prt.events.PEvent<Integer> {
        public eMonitor_AtomicityInitialize(int p) { this.payload = p; }
        private Integer payload;
        public Integer getPayload() { return payload; }

        @Override
        public String toString() {
            return "eMonitor_AtomicityInitialize[" + payload + "]";
        } // toString()

    } // PEvent definition for eMonitor_AtomicityInitialize
    public static class eStartTimer extends prt.events.PEvent<Void> {
        public eStartTimer() { }
        private Void payload;
        public Void getPayload() { return payload; }

        @Override
        public String toString() {
            return "eStartTimer";
        } // toString()

    } // PEvent definition for eStartTimer
    public static class eCancelTimer extends prt.events.PEvent<Void> {
        public eCancelTimer() { }
        private Void payload;
        public Void getPayload() { return payload; }

        @Override
        public String toString() {
            return "eCancelTimer";
        } // toString()

    } // PEvent definition for eCancelTimer
    public static class eTimeOut extends prt.events.PEvent<Void> {
        public eTimeOut() { }
        private Void payload;
        public Void getPayload() { return payload; }

        @Override
        public String toString() {
            return "eTimeOut";
        } // toString()

    } // PEvent definition for eTimeOut
    public static class eDelayedTimeOut extends prt.events.PEvent<Void> {
        public eDelayedTimeOut() { }
        private Void payload;
        public Void getPayload() { return payload; }

        @Override
        public String toString() {
            return "eDelayedTimeOut";
        } // toString()

    } // PEvent definition for eDelayedTimeOut
    public static class eDelayNodeFailure extends prt.events.PEvent<Void> {
        public eDelayNodeFailure() { }
        private Void payload;
        public Void getPayload() { return payload; }

        @Override
        public String toString() {
            return "eDelayNodeFailure";
        } // toString()

    } // PEvent definition for eDelayNodeFailure
    public static class eShutDown extends prt.events.PEvent<Long> {
        public eShutDown(long p) { this.payload = p; }
        private Long payload;
        public Long getPayload() { return payload; }

        @Override
        public String toString() {
            return "eShutDown[" + payload + "]";
        } // toString()

    } // PEvent definition for eShutDown

    // PMachine Coordinator elided
    // PMachine Participant elided
    public static class AtomicityInvariant extends prt.Monitor {

        public void reInitializeMonitor() {}; // dummy implementation.

        public List<Class<? extends PEvent<?>>> getEventTypes() { return List.of(); } //XXX: dummy implementation.

        private HashMap<Integer,HashMap<Integer,Integer>> participantsResponse = new HashMap<Integer,HashMap<Integer,Integer>>();
        public HashMap<Integer,HashMap<Integer,Integer>> get_participantsResponse() { return this.participantsResponse; };

        private int numParticipants = 0;
        public int get_numParticipants() { return this.numParticipants; };


        private enum States {
            INIT_STATE,
            WAITFOREVENTS_STATE
        }

        private void Anon(int n) {
            numParticipants = n;
        }
        private void Anon_1(PTuple_prtcp_trans_stts resp) {
            int transId = 0;
            int TMP_tmp0 = 0;
            int TMP_tmp1 = 0;
            boolean TMP_tmp2 = false;
            boolean TMP_tmp3 = false;
            HashMap<Integer,Integer> TMP_tmp4 = new HashMap<Integer,Integer>();
            int TMP_tmp5 = 0;
            HashMap<Integer,Integer> TMP_tmp6 = new HashMap<Integer,Integer>();
            int TMP_tmp7 = 0;
            int TMP_tmp8 = 0;
            int TMP_tmp9 = 0;

            TMP_tmp0 = resp.transId;
            TMP_tmp1 = TMP_tmp0;
            transId = TMP_tmp1;
            TMP_tmp2 = participantsResponse.containsKey(transId);
            TMP_tmp3 = !(TMP_tmp2);
            if (TMP_tmp3) {
                TMP_tmp4 = new HashMap<Integer,Integer>();
                participantsResponse.put(transId,TMP_tmp4);
                participantsResponse.get(transId).put(tTransStatus.SUCCESS,0);
                participantsResponse.get(transId).put(tTransStatus.ERROR,0);
            }
            TMP_tmp5 = resp.status;
            TMP_tmp6 = participantsResponse.get(transId);
            TMP_tmp7 = resp.status;
            TMP_tmp8 = TMP_tmp6.get(TMP_tmp7);
            TMP_tmp9 = TMP_tmp8 + 1;
            participantsResponse.get(transId).put(TMP_tmp5,TMP_tmp9);
        }
        private void Anon_2(PTuple_trans_stts resp_1) {
            int TMP_tmp0_1 = 0;
            boolean TMP_tmp1_1 = false;
            int TMP_tmp2_1 = 0;
            boolean TMP_tmp3_1 = false;
            boolean TMP_tmp4_1 = false;
            String TMP_tmp5_1 = "";
            int TMP_tmp6_1 = 0;
            boolean TMP_tmp7_1 = false;
            int TMP_tmp8_1 = 0;
            HashMap<Integer,Integer> TMP_tmp9_1 = new HashMap<Integer,Integer>();
            int TMP_tmp10 = 0;
            boolean TMP_tmp11 = false;
            String TMP_tmp12 = "";
            int TMP_tmp13 = 0;
            HashMap<Integer,Integer> TMP_tmp14 = new HashMap<Integer,Integer>();
            int TMP_tmp15 = 0;
            int TMP_tmp16 = 0;
            HashMap<Integer,Integer> TMP_tmp17 = new HashMap<Integer,Integer>();
            int TMP_tmp18 = 0;
            String TMP_tmp19 = "";
            String TMP_tmp20 = "";
            int TMP_tmp21 = 0;
            boolean TMP_tmp22 = false;
            int TMP_tmp23 = 0;
            HashMap<Integer,Integer> TMP_tmp24 = new HashMap<Integer,Integer>();
            int TMP_tmp25 = 0;
            boolean TMP_tmp26 = false;
            int TMP_tmp27 = 0;
            String TMP_tmp28 = "";
            int TMP_tmp29 = 0;
            HashMap<Integer,Integer> TMP_tmp30 = new HashMap<Integer,Integer>();
            int TMP_tmp31 = 0;
            int TMP_tmp32 = 0;
            HashMap<Integer,Integer> TMP_tmp33 = new HashMap<Integer,Integer>();
            int TMP_tmp34 = 0;
            String TMP_tmp35 = "";
            String TMP_tmp36 = "";
            int TMP_tmp37 = 0;

            TMP_tmp0_1 = resp_1.transId;
            TMP_tmp1_1 = participantsResponse.containsKey(TMP_tmp0_1);
            TMP_tmp4_1 = TMP_tmp1_1;
            if (TMP_tmp4_1) {} else
            {
                TMP_tmp2_1 = resp_1.status;
                TMP_tmp3_1 = TMP_tmp2_1 == tTransStatus.TIMEOUT;
                TMP_tmp4_1 = TMP_tmp3_1;
            }
            TMP_tmp5_1 = "Write transaction was responded to the client without receiving any responses from the participants!";
            tryAssert(TMP_tmp4_1, TMP_tmp5_1);
            TMP_tmp6_1 = resp_1.status;
            TMP_tmp7_1 = TMP_tmp6_1 == tTransStatus.SUCCESS;
            if (TMP_tmp7_1) {
                TMP_tmp8_1 = resp_1.transId;
                TMP_tmp9_1 = participantsResponse.get(TMP_tmp8_1);
                TMP_tmp10 = TMP_tmp9_1.get(tTransStatus.SUCCESS);
                TMP_tmp11 = TMP_tmp10 == numParticipants;
                TMP_tmp12 = "Write transaction was responded as committed before receiving success from all participants. ";
                TMP_tmp13 = resp_1.transId;
                TMP_tmp14 = participantsResponse.get(TMP_tmp13);
                TMP_tmp15 = TMP_tmp14.get(tTransStatus.SUCCESS);
                TMP_tmp16 = resp_1.transId;
                TMP_tmp17 = participantsResponse.get(TMP_tmp16);
                TMP_tmp18 = TMP_tmp17.get(tTransStatus.ERROR);
                TMP_tmp19 = MessageFormat.format("participants sent success: {0}, participants sent error: {1}", TMP_tmp15, TMP_tmp18);
                TMP_tmp20 = TMP_tmp12 + TMP_tmp19;
                tryAssert(TMP_tmp11, TMP_tmp20);
            }
            else
            {
                TMP_tmp21 = resp_1.status;
                TMP_tmp22 = TMP_tmp21 == tTransStatus.ERROR;
                if (TMP_tmp22) {
                    TMP_tmp23 = resp_1.transId;
                    TMP_tmp24 = participantsResponse.get(TMP_tmp23);
                    TMP_tmp25 = TMP_tmp24.get(tTransStatus.ERROR);
                    TMP_tmp26 = TMP_tmp25 > 0;
                    TMP_tmp27 = resp_1.transId;
                    TMP_tmp28 = MessageFormat.format("Write transaction {0} was responded as failed before receiving error from atleast one participant.", TMP_tmp27);
                    TMP_tmp29 = resp_1.transId;
                    TMP_tmp30 = participantsResponse.get(TMP_tmp29);
                    TMP_tmp31 = TMP_tmp30.get(tTransStatus.SUCCESS);
                    TMP_tmp32 = resp_1.transId;
                    TMP_tmp33 = participantsResponse.get(TMP_tmp32);
                    TMP_tmp34 = TMP_tmp33.get(tTransStatus.ERROR);
                    TMP_tmp35 = MessageFormat.format("participants sent success: {0}, participants sent error: {1}", TMP_tmp31, TMP_tmp34);
                    TMP_tmp36 = TMP_tmp28 + TMP_tmp35;
                    tryAssert(TMP_tmp26, TMP_tmp36);
                }
            }
            TMP_tmp37 = resp_1.transId;
            participantsResponse.remove(TMP_tmp37);
        }

        public AtomicityInvariant() {
            super();
            addState(prt.State.keyedOn(States.INIT_STATE)
                    .isInitialState(true)
                    .withEvent(eMonitor_AtomicityInitialize.class, p -> { Anon(p); gotoState(States.WAITFOREVENTS_STATE); })
                    .build());
            addState(prt.State.keyedOn(States.WAITFOREVENTS_STATE)
                    .isInitialState(false)
                    .withEvent(ePrepareResp.class, this::Anon_1)
                    .withEvent(eWriteTransResp.class, this::Anon_2)
                    .build());
        } // constructor
    } // AtomicityInvariant monitor definition
    public static class Progress extends prt.Monitor {

        public void reInitializeMonitor() {}; // dummy implementation.

        public List<Class<? extends PEvent<?>>> getEventTypes() { return List.of(); } //XXX: dummy implementation.

        private int pendingTransactions = 0;
        public int get_pendingTransactions() { return this.pendingTransactions; };


        private enum States {
            INIT_STATE,
            WAITFORRESPONSES_STATE,
            ALLTRANSACTIONSFINISHED_STATE
        }

        private void Anon_3() {
            int TMP_tmp0_2 = 0;

            TMP_tmp0_2 = pendingTransactions + 1;
            pendingTransactions = TMP_tmp0_2;
        }
        private void Anon_4()throws TransitionException {
            int TMP_tmp0_3 = 0;
            boolean TMP_tmp1_2 = false;

            TMP_tmp0_3 = pendingTransactions - 1;
            pendingTransactions = TMP_tmp0_3;
            TMP_tmp1_2 = pendingTransactions == 0;
            if (TMP_tmp1_2) {
                gotoState(States.ALLTRANSACTIONSFINISHED_STATE);
                return;
            }
        }
        private void Anon_5() {
            int TMP_tmp0_4 = 0;

            TMP_tmp0_4 = pendingTransactions + 1;
            pendingTransactions = TMP_tmp0_4;
        }
        private void Anon_6() {
            int TMP_tmp0_5 = 0;

            TMP_tmp0_5 = pendingTransactions + 1;
            pendingTransactions = TMP_tmp0_5;
        }

        public Progress() {
            super();
            addState(prt.State.keyedOn(States.INIT_STATE)
                    .isInitialState(true)
                    .withEvent(eWriteTransReq.class, __ -> { Anon_3(); gotoState(States.WAITFORRESPONSES_STATE); })
                    .build());
            addState(prt.State.keyedOn(States.WAITFORRESPONSES_STATE)
                    .isInitialState(false)
                    .withEvent(eWriteTransResp.class, __ -> Anon_4())
                    .withEvent(eWriteTransReq.class, __ -> Anon_5())
                    .build());
            addState(prt.State.keyedOn(States.ALLTRANSACTIONSFINISHED_STATE)
                    .isInitialState(false)
                    .withEvent(eWriteTransReq.class, __ -> { Anon_6(); gotoState(States.WAITFORRESPONSES_STATE); })
                    .build());
        } // constructor
    } // Progress monitor definition
    // PMachine SingleClientNoFailure elided
    // PMachine MultipleClientsNoFailure elided
    // PMachine MultipleClientsWithFailure elided
    // PMachine Client elided
    // PMachine Timer elided
    // PMachine FailureInjector elided
} // TwoPhaseCommit.java class definition
