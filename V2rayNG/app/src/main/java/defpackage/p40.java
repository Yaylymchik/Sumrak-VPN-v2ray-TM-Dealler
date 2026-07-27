package defpackage;

import java.util.ArrayList;
import java.util.Arrays;
import java.util.Collections;
import java.util.List;

/** Minimal Happ compat shims for crypt5 native callbacks. */
public final class p40 {
    private p40() {
    }

    public static List<Object> k0(Object... values) {
        if (values.length == 0) {
            return Collections.emptyList();
        }
        return new ArrayList<>(Arrays.asList(values));
    }
}
