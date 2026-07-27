package defpackage;

import java.util.ArrayList;

/** Minimal Happ compat shims for crypt5 native callbacks. */
public final class df {
    private df() {
    }

    public static String C(String str) {
        ArrayList<String> arrayList = new ArrayList<>();
        for (String chunk : bk4.X0(str, 8, 8, true)) {
            if (chunk.length() > 7) {
                StringBuilder sb = new StringBuilder();
                sb.append(chunk.charAt(5));
                sb.append(chunk.charAt(7));
                sb.append(chunk.charAt(1));
                sb.append(chunk.charAt(0));
                sb.append(chunk.charAt(2));
                sb.append(chunk.charAt(3));
                sb.append(chunk.charAt(6));
                sb.append(chunk.charAt(4));
                arrayList.add(sb.toString());
            } else {
                arrayList.add(chunk);
            }
        }
        return o40.M0(arrayList, "", null, null, null, 62);
    }
}
