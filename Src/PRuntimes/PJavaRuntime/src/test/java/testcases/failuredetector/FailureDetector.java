package testcases.failuredetector;

/***************************************************************************
 * This file was auto-generated on Wednesday, 22 June 2022 at 11:29:15.
 * Please do not edit manually!
 **************************************************************************/

import prt.events.PEvent;
import prt.exceptions.TransitionException;

import java.util.*;


public class FailureDetector {
    /* Tuples */
    // (fd:FailureDetector,trial:int)
    public static class PTuple_fd_trial implements prt.values.PValue<PTuple_fd_trial> {
        public long fd;
        public int trial;

        public PTuple_fd_trial() {
            this.fd = 0L;
            this.trial = 0;
        }

        public PTuple_fd_trial(long fd, int trial) {
            this.fd = fd;
            this.trial = trial;
        }

        public PTuple_fd_trial deepClone() {
            return new PTuple_fd_trial(fd, trial);
        } // deepClone()

        public boolean equals(Object other) {
            return (this.getClass() == other.getClass() &&
                    this.deepEquals((PTuple_fd_trial)other)
            );
        } // equals()

        public boolean deepEquals(PTuple_fd_trial other) {
            return (true
                    && this.fd == other.fd
                    && this.trial == other.trial
            );
        } // deepEquals()

        public String toString() {
            StringBuilder sb = new StringBuilder("PTuple_fd_trial");
            sb.append("[");
            sb.append("fd=" + fd);
            sb.append(",trial=" + trial);
            sb.append("]");
            return sb.toString();
        } // toString()
    } //PTuple_fd_trial class definition

    // (node:Node,trial:int)
    public static class PTuple_node_trial implements prt.values.PValue<PTuple_node_trial> {
        public long node;
        public int trial;

        public PTuple_node_trial() {
            this.node = 0L;
            this.trial = 0;
        }

        public PTuple_node_trial(long node, int trial) {
            this.node = node;
            this.trial = trial;
        }

        public PTuple_node_trial deepClone() {
            return new PTuple_node_trial(node, trial);
        } // deepClone()

        public boolean equals(Object other) {
            return (this.getClass() == other.getClass() &&
                    this.deepEquals((PTuple_node_trial)other)
            );
        } // equals()

        public boolean deepEquals(PTuple_node_trial other) {
            return (true
                    && this.node == other.node
                    && this.trial == other.trial
            );
        } // deepEquals()

        public String toString() {
            StringBuilder sb = new StringBuilder("PTuple_node_trial");
            sb.append("[");
            sb.append("node=" + node);
            sb.append(",trial=" + trial);
            sb.append("]");
            return sb.toString();
        } // toString()
    } //PTuple_node_trial class definition

    // (numNodes:int,numClients:int)
    public static class PTuple_nmnds_nmcln implements prt.values.PValue<PTuple_nmnds_nmcln> {
        public int numNodes;
        public int numClients;

        public PTuple_nmnds_nmcln() {
            this.numNodes = 0;
            this.numClients = 0;
        }

        public PTuple_nmnds_nmcln(int numNodes, int numClients) {
            this.numNodes = numNodes;
            this.numClients = numClients;
        }

        public PTuple_nmnds_nmcln deepClone() {
            return new PTuple_nmnds_nmcln(numNodes, numClients);
        } // deepClone()

        public boolean equals(Object other) {
            return (this.getClass() == other.getClass() &&
                    this.deepEquals((PTuple_nmnds_nmcln)other)
            );
        } // equals()

        public boolean deepEquals(PTuple_nmnds_nmcln other) {
            return (true
                    && this.numNodes == other.numNodes
                    && this.numClients == other.numClients
            );
        } // deepEquals()

        public String toString() {
            StringBuilder sb = new StringBuilder("PTuple_nmnds_nmcln");
            sb.append("[");
            sb.append("numNodes=" + numNodes);
            sb.append(",numClients=" + numClients);
            sb.append("]");
            return sb.toString();
        } // toString()
    } //PTuple_nmnds_nmcln class definition

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
    public static class ePing extends prt.events.PEvent<PTuple_fd_trial> {
        public ePing(PTuple_fd_trial p) { this.payload = p; }
        private PTuple_fd_trial payload;
        public PTuple_fd_trial getPayload() { return payload; }

        @Override
        public String toString() {
            return "ePing[" + payload + "]";
        } // toString()

    } // PEvent definition for ePing
    public static class ePong extends prt.events.PEvent<PTuple_node_trial> {
        public ePong(PTuple_node_trial p) { this.payload = p; }
        private PTuple_node_trial payload;
        public PTuple_node_trial getPayload() { return payload; }

        @Override
        public String toString() {
            return "ePong[" + payload + "]";
        } // toString()

    } // PEvent definition for ePong
    public static class eNotifyNodesDown extends prt.events.PEvent<LinkedHashSet<Long>> {
        public eNotifyNodesDown(LinkedHashSet<Long> p) { this.payload = p; }
        private LinkedHashSet<Long> payload;
        public LinkedHashSet<Long> getPayload() { return payload; }

        @Override
        public String toString() {
            return "eNotifyNodesDown[" + payload + "]";
        } // toString()

    } // PEvent definition for eNotifyNodesDown
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

    // PMachine Client elided
    // PMachine FailureDetector elided
    // PMachine Node elided
    public static class ReliableFailureDetector extends prt.Monitor {

        public void reInitializeMonitor() {} // dummy implementation.

        public List<Class<? extends PEvent<?>>> getEventTypes() { return List.of(); } //XXX: dummy implementation.

        private LinkedHashSet<Long> nodesShutdownAndNotDetected = new LinkedHashSet<Long>();
        public LinkedHashSet<Long> get_nodesShutdownAndNotDetected() { return this.nodesShutdownAndNotDetected; };

        private LinkedHashSet<Long> nodesDownDetected = new LinkedHashSet<Long>();
        public LinkedHashSet<Long> get_nodesDownDetected() { return this.nodesDownDetected; };


        private enum States {
            ALLSHUTDOWNNODESAREDETECTED_STATE,
            NODESSHUTDOWNBUTNOTDETECTED_STATE
        }

        private void Anon(LinkedHashSet<Long> nodes) {
            int i = 0;
            int TMP_tmp0 = 0;
            boolean TMP_tmp1 = false;
            boolean TMP_tmp2 = false;
            long TMP_tmp3 = 0L;
            long TMP_tmp4 = 0L;
            int TMP_tmp5 = 0;

            while ((true)) {
                TMP_tmp0 = nodes.size();
                TMP_tmp1 = i < TMP_tmp0;
                TMP_tmp2 = TMP_tmp1;
                if (TMP_tmp2) {} else
                {
                    break;
                }
                TMP_tmp3 = prt.values.SetIndexing.elementAt(nodes, i);
                nodesShutdownAndNotDetected.remove(TMP_tmp3);
                TMP_tmp4 = prt.values.SetIndexing.elementAt(nodes, i);
                nodesDownDetected.add(TMP_tmp4);
                TMP_tmp5 = i + 1;
                i = TMP_tmp5;
            }
        }
        private void Anon_1(long node)throws TransitionException {
            long TMP_tmp0_1 = 0L;
            boolean TMP_tmp1_1 = false;
            boolean TMP_tmp2_1 = false;
            long TMP_tmp3_1 = 0L;

            TMP_tmp0_1 = ((long)node);
            TMP_tmp1_1 = nodesDownDetected.contains(TMP_tmp0_1);
            TMP_tmp2_1 = !(TMP_tmp1_1);
            if (TMP_tmp2_1) {
                TMP_tmp3_1 = ((long)node);
                nodesShutdownAndNotDetected.add(TMP_tmp3_1);
                gotoState(States.NODESSHUTDOWNBUTNOTDETECTED_STATE);
                return;
            }
        }
        private void Anon_2(LinkedHashSet<Long> nodes_1)throws TransitionException {
            int i_1 = 0;
            int TMP_tmp0_2 = 0;
            boolean TMP_tmp1_2 = false;
            boolean TMP_tmp2_2 = false;
            long TMP_tmp3_2 = 0L;
            long TMP_tmp4_1 = 0L;
            int TMP_tmp5_1 = 0;
            int TMP_tmp6 = 0;
            boolean TMP_tmp7 = false;

            while ((true)) {
                TMP_tmp0_2 = nodes_1.size();
                TMP_tmp1_2 = i_1 < TMP_tmp0_2;
                TMP_tmp2_2 = TMP_tmp1_2;
                if (TMP_tmp2_2) {} else
                {
                    break;
                }
                TMP_tmp3_2 = prt.values.SetIndexing.elementAt(nodes_1, i_1);
                nodesShutdownAndNotDetected.remove(TMP_tmp3_2);
                TMP_tmp4_1 = prt.values.SetIndexing.elementAt(nodes_1, i_1);
                nodesDownDetected.add(TMP_tmp4_1);
                TMP_tmp5_1 = i_1 + 1;
                i_1 = TMP_tmp5_1;
            }
            TMP_tmp6 = nodesShutdownAndNotDetected.size();
            TMP_tmp7 = TMP_tmp6 == 0;
            if (TMP_tmp7) {
                gotoState(States.ALLSHUTDOWNNODESAREDETECTED_STATE);
                return;
            }
        }
        private void Anon_3(long node_1) {
            long TMP_tmp0_3 = 0L;
            boolean TMP_tmp1_3 = false;
            boolean TMP_tmp2_3 = false;
            long TMP_tmp3_3 = 0L;

            TMP_tmp0_3 = ((long)node_1);
            TMP_tmp1_3 = nodesDownDetected.contains(TMP_tmp0_3);
            TMP_tmp2_3 = !(TMP_tmp1_3);
            if (TMP_tmp2_3) {
                TMP_tmp3_3 = ((long)node_1);
                nodesShutdownAndNotDetected.add(TMP_tmp3_3);
            }
        }

        public ReliableFailureDetector() {
            super();
            addState(prt.State.keyedOn(States.ALLSHUTDOWNNODESAREDETECTED_STATE)
                    .isInitialState(true)
                    .withEvent(eNotifyNodesDown.class, this::Anon)
                    .withEvent(eShutDown.class, this::Anon_1)
                    .build());
            addState(prt.State.keyedOn(States.NODESSHUTDOWNBUTNOTDETECTED_STATE)
                    .isInitialState(false)
                    .withEvent(eNotifyNodesDown.class, this::Anon_2)
                    .withEvent(eShutDown.class, this::Anon_3)
                    .build());
        } // constructor
    } // ReliableFailureDetector monitor definition
    // PMachine TestMultipleClients elided
    // PMachine Timer elided
    // PMachine FailureInjector elided
} // FailureDetector.java class definition
