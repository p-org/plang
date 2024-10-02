using System;

namespace PChecker.SystematicTesting;

public class Timestamp : IComparable<Timestamp>
{
    private double Time;

    private object TimeLock;

    /// <summary>
    /// Default Timestamp object.
    /// </summary>
    public static readonly Timestamp DefaultTimestamp = new(-1);

    /// <summary>
    /// Constructor.
    /// </summary>
    public Timestamp()
    {
        Time = 0;
        TimeLock = new object();
    }

    private Timestamp(double time)
    {
        Time = time;
        TimeLock = new object();
    }

    /// <summary>
    /// Gets Time.
    /// </summary>
    public double GetTime()
    {
        double time;
        lock (TimeLock)
        {
            time = Time;

        }
        return time;
    }

    /// <summary>
    /// Sets Time.
    /// </summary>
    /// <param name="time">Value to set.</param>
    public void SetTime(double time)
    {
        lock (TimeLock)
        {
            Time = time;

        }
    }

    /// <summary>
    /// Increments Time with given delay value.
    /// </summary>
    /// <param name="delay">Delay added to Time.</param>
    public void IncrementTime(double delay)
    {
        lock (TimeLock)
        {
            Time += delay;

        }
    }

    /// <summary>
    /// Checks greater than.
    /// </summary>
    /// <param name="t1">Left-hand side value.</param>
    /// <param name="t2">Right-hand side value.</param>
    /// <returns>Result of the greater than check.</returns>
    public static bool operator>(Timestamp t1, Timestamp t2)
    {
        return t1.GetTime() > t2.GetTime();
    }

    /// <summary>
    /// Checks greater than or equal to.
    /// </summary>
    /// <param name="t1">Left-hand side value.</param>
    /// <param name="t2">Right-hand side value.</param>
    /// <returns>Result of the greater than or equal to check.</returns>
    public static bool operator>=(Timestamp t1, Timestamp t2)
    {
        return t1.GetTime() >= t2.GetTime();
    }

    /// <summary>
    /// Checks less than.
    /// </summary>
    /// <param name="t1">Left-hand side value.</param>
    /// <param name="t2">Right-hand side value.</param>
    /// <returns>Result of the less than check.</returns>
    public static bool operator<(Timestamp t1, Timestamp t2)
    {
        return t1.GetTime() < t2.GetTime();
    }

    /// <summary>
    /// Checks less than or equal to.
    /// </summary>
    /// <param name="t1">Left-hand side value.</param>
    /// <param name="t2">Right-hand side value.</param>
    /// <returns>Result of the less than or equal to check.</returns>
    public static bool operator<=(Timestamp t1, Timestamp t2)
    {
        return t1.GetTime() <= t2.GetTime();
    }

    /// <summary>
    /// Checks equality.
    /// </summary>
    /// <param name="t1">Left-hand side value.</param>
    /// <param name="t2">Right-hand side value.</param>
    /// <returns>Result of the equality check.</returns>
    public static bool operator==(Timestamp t1, Timestamp t2)
    {
        return t1.GetTime() == t2.GetTime();
    }

    /// <summary>
    /// Checks inequality.
    /// </summary>
    /// <param name="t1">Left-hand side value.</param>
    /// <param name="t2">Right-hand side value.</param>
    /// <returns>Result of the inequality check.</returns>
    public static bool operator!=(Timestamp t1, Timestamp t2)
    {
        return t1.GetTime() != t2.GetTime();
    }

    /// <inheritdoc />
    public int CompareTo(Timestamp other)
    {
        if (ReferenceEquals(this, other)) return 0;
        if (ReferenceEquals(null, other)) return 1;
        return Time.CompareTo(other.Time);
    }

    private bool Equals(Timestamp other)
    {
        return Time.Equals(other.Time);
    }

    /// <inheritdoc />
    public override bool Equals(object obj)
    {
        if (ReferenceEquals(null, obj)) return false;
        if (ReferenceEquals(this, obj)) return true;
        if (obj.GetType() != this.GetType()) return false;
        return Equals((Timestamp)obj);
    }

    /// <inheritdoc />
    public override int GetHashCode()
    {
        return Time.GetHashCode();
    }
}
