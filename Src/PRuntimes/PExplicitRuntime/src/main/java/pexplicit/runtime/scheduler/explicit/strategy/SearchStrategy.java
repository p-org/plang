package pexplicit.runtime.scheduler.explicit.strategy;

import lombok.Getter;
import pexplicit.runtime.PExplicitGlobal;
import pexplicit.runtime.logger.PExplicitLogger;
import pexplicit.runtime.scheduler.Choice;
import pexplicit.runtime.scheduler.explicit.SearchStatistics;

import java.io.Serializable;
import java.util.ArrayList;
import java.util.HashSet;
import java.util.List;
import java.util.Set;

@Getter
public abstract class SearchStrategy implements Serializable {
    /**
     * List of all search tasks
     */
    final List<SearchTask> allTasks = new ArrayList<>();
    /**
     * Set of all search tasks that are pending
     */
    final Set<Integer> pendingTasks = new HashSet<>();
    /**
     * List of all search tasks that finished
     */
    final List<Integer> finishedTasks = new ArrayList<>();
    /**
     * Task id of the latest search task
     */
    int currTaskId = 0;
    /**
     * Starting iteration number for the current task
     */
    int currTaskStartIteration = 0;

    public SearchTask createTask(Choice choice, int choiceNum, SearchTask parentTask) {
        SearchTask newTask = new SearchTask(allTasks.size(), choice, choiceNum, parentTask);
        allTasks.add(newTask);
        pendingTasks.add(newTask.getId());
        return newTask;
    }

    private void setCurrTask(SearchTask task) {
        assert (pendingTasks.contains(task.getId()));
        pendingTasks.remove(task.getId());
        currTaskId = task.getId();
        currTaskStartIteration = SearchStatistics.iteration;
    }

    public void createFirstTask() {
        assert (allTasks.size() == 0);
        SearchTask firstTask = createTask(null, 0, null);
        setCurrTask(firstTask);
    }

    public SearchTask getCurrTask() {
        return getTask(currTaskId);
    }


    public int getNumSchedulesInCurrTask() {
        return SearchStatistics.iteration - currTaskStartIteration;
    }



    private boolean isValidTaskId(int id) {
        return (id < allTasks.size());
    }

    protected SearchTask getTask(int id) {
        assert (isValidTaskId(id));
        return allTasks.get(id);
    }

    public SearchTask setNextTask() {
        if (pendingTasks.isEmpty()) {
            return null;
        }

        SearchTask nextTask = popNextTask();
        setCurrTask(nextTask);

        return nextTask;
    }

    /**
     * Get the number of unexplored choices in the pending tasks
     *
     * @return Number of unexplored choices
     */
    public int getNumPendingChoices() {
        int numUnexplored = 0;
        for (Integer tid: pendingTasks) {
            SearchTask task = getTask(tid);
            numUnexplored += task.getNumUnexploredScheduleChoices() + task.getNumUnexploredDataChoices();
        }
        return numUnexplored;
    }

    /**
     * Get the number of unexplored data choices in the pending tasks
     *
     * @return Number of unexplored data choices
     */
    public int getNumPendingDataChoices() {
        int numUnexplored = 0;
        for (Integer tid: pendingTasks) {
            SearchTask task = getTask(tid);
            numUnexplored += task.getNumUnexploredDataChoices();
        }
        return numUnexplored;
    }

    public abstract void addNewTask(SearchTask task);

    abstract SearchTask popNextTask();
}
