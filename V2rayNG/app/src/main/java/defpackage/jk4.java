package defpackage;

/** Minimal Happ compat shims for crypt5 native callbacks. */
public final class jk4 {
    private jk4() {
    }

    public static boolean b0(String str, boolean ignoreCase, String suffix) {
        if (ignoreCase) {
            return str.regionMatches(true, str.length() - suffix.length(), suffix, 0, suffix.length());
        }
        return str.endsWith(suffix);
    }

    public static boolean i0(String str, boolean ignoreCase, String prefix) {
        if (ignoreCase) {
            return str.regionMatches(true, 0, prefix, 0, prefix.length());
        }
        return str.startsWith(prefix);
    }
}
