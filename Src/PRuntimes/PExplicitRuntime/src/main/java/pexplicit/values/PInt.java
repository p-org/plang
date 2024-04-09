package pexplicit.values;

import lombok.Getter;

/**
 * Represents the PValue for P integer
 */
@Getter
public class PInt extends PValue<PInt> {
    private final int value;

    /**
     * Constructor
     *
     * @param val integer value to set to
     */
    public PInt(int val) {
        value = val;
    }

    /**
     * Constructor
     *
     * @param val object from where value to set to
     */
    public PInt(Object val) {
        if (val instanceof PInt)
            value = ((PInt) val).value;
        else
            value = (int) val;
    }

    /**
     * Copy constructor
     *
     * @param val value to copy from
     */
    public PInt(PInt val) {
        value = val.value;
    }

    /**
     * Negation operation
     * @return Result after operation
     */
    public PInt negate() {
        return new PInt(-value);
    }

    /**
     * Add operation
     * @param val value to add
     * @return Result after addition
     */
    public PInt add(PInt val) {
        return new PInt(value + val.value);
    }

    /**
     * Subtract operation
     * @param val value to subtract
     * @return Result after subtraction
     */
    public PInt sub(PInt val) {
        return new PInt(value - val.value);
    }

    /**
     * Multiply operation
     * @param val value to multiply
     * @return Result after multiplication
     */
    public PInt mul(PInt val) {
        return new PInt(value * val.value);
    }

    /**
     * Divide operation
     * @param val value to divide
     * @return Result after division
     */
    public PInt div(PInt val) {
        return new PInt(value / val.value);
    }

    /**
     * Modulo operation
     * @param val value to modulo
     * @return Result after modulo
     */
    public PInt mod(PInt val) {
        return new PInt(value % val.value);
    }

    /**
     * Less than operation
     * @param val value to compare to
     * @return PBool object after operation
     */
    public PBool lt(PInt val) {
        return new PBool(value < val.value);
    }

    /**
     * Less than or equal to operation
     * @param val value to compare to
     * @return PBool object after operation
     */
    public PBool le(PInt val) {
        return new PBool(value <= val.value);
    }

    /**
     * Greater than operation
     * @param val value to compare to
     * @return PBool object after operation
     */
    public PBool gt(PInt val) {
        return new PBool(value > val.value);
    }

    /**
     * Greater than or equal to operation
     * @param val value to compare to
     * @return PBool object after operation
     */
    public PBool ge(PInt val) {
        return new PBool(value >= val.value);
    }

    @Override
    public PInt clone() {
        return new PInt(value);
    }

    @Override
    public int hashCode() {
        return ((Integer) value).hashCode();
    }

    @Override
    public boolean equals(Object obj) {
        if (obj == this)
            return true;
        else if (!(obj instanceof PInt)) {
            return false;
        }
        return this.value == ((PInt) obj).value;
    }

    @Override
    public String toString() {
        return Long.toString(value);
    }
}