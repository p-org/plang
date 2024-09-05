package pex.utils.monitor;

import com.sun.management.GarbageCollectionNotificationInfo;
import lombok.Getter;
import pex.utils.exceptions.MemoutException;

import javax.management.NotificationEmitter;
import javax.management.NotificationListener;
import java.lang.management.GarbageCollectorMXBean;
import java.lang.management.ManagementFactory;

public class MemoryMonitor {
    private static NotificationListener notificationListener;
    @Getter
    private static double maxMemSpent = 0; // max memory in megabytes
    @Getter
    private static double memSpent = 0; // max memory in megabytes
    @Getter
    private static double memLimit = 0; // memory limit in megabytes (0 means infinite)

    public static void setup(double ml) {
        memSpent = 0;
        maxMemSpent = 0;
        memLimit = ml;

        notificationListener =
                (notification, handback) -> {
                    if (notification
                            .getType()
                            .equals(GarbageCollectionNotificationInfo.GARBAGE_COLLECTION_NOTIFICATION)) {
                        Runtime runtime = Runtime.getRuntime();
                        memSpent = (runtime.totalMemory() - runtime.freeMemory()) / 1000000.0;
                        if (maxMemSpent < memSpent) maxMemSpent = memSpent;
                    }
                };

        // register our listener with all gc beans
        for (GarbageCollectorMXBean gcBean : ManagementFactory.getGarbageCollectorMXBeans()) {
            NotificationEmitter emitter = (NotificationEmitter) gcBean;
            emitter.addNotificationListener(notificationListener, null, null);
        }
    }

    public static void checkMemout() throws MemoutException {
        if (memLimit > 0) {
            if (MemoryMonitor.getMemSpent() > memLimit) {
                throw new MemoutException(
                        String.format("Max memory limit reached: %.1f MB", MemoryMonitor.getMemSpent()),
                        MemoryMonitor.getMemSpent());
            }
        }
    }
}
