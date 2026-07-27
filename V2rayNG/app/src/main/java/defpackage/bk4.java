package defpackage;

import java.util.ArrayList;
import java.util.Arrays;
import java.util.List;

/** Minimal Happ compat shims for crypt5 native callbacks. */
public final class bk4 {
    private bk4() {
    }

    public static StringBuilder H0(String str) {
        return new StringBuilder(str).reverse();
    }

    public static ArrayList<String> X0(CharSequence charSequence, int size, int step, boolean includePartial) {
        int length = charSequence.length();
        ArrayList<String> out = new ArrayList<>((length / step) + 1);
        int index = 0;
        while (index >= 0 && index < length) {
            int end = index + size;
            if (end < 0 || end > length) {
                if (!includePartial) {
                    break;
                }
                end = length;
            }
            out.add(charSequence.subSequence(index, end).toString());
            index += step;
        }
        return out;
    }
}
