package defpackage;

/** Minimal Happ compat shims for crypt5 native callbacks. */
public final class o40 {
    private o40() {
    }

    public static String M0(
            Iterable<?> iterable,
            CharSequence separator,
            String prefix,
            String postfix,
            Object transform,
            int flags
    ) {
        StringBuilder sb = new StringBuilder();
        for (Object item : iterable) {
            sb.append(item);
        }
        return sb.toString();
    }
}
