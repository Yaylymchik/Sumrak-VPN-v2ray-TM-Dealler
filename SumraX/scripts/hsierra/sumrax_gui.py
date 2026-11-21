#!/usr/bin/env python3
# Sumrak VPN — GUI like (tkinter, macOS 10.13+, no .NET)
import os
import subprocess
import sys
import tkinter as tk
from tkinter import font as tkfont
from tkinter import messagebox, scrolledtext

BG = "#0f1419"
PANEL = "#1a2332"
ACCENT = "#00a8ff"
GREEN = "#2ecc71"
RED = "#e74c3c"
TEXT = "#e8eef4"
MUTED = "#8b9cb3"


def app_root():
    env = os.environ.get("SUMRAX_ROOT")
    if env and os.path.isdir(env):
        return env
    return os.path.dirname(os.path.abspath(__file__))


ROOT = app_root()
SUMRAX = os.path.join(ROOT, "sumrax")
CONFIG = os.path.join(ROOT, "config", "config.json")
if not os.path.isfile(os.path.join(ROOT, "sumrax")) and os.path.isfile(
    os.path.join(os.path.dirname(__file__), "sumrax")
):
    ROOT = os.path.dirname(os.path.abspath(__file__))
    SUMRAX = os.path.join(ROOT, "sumrax")
    CONFIG = os.path.join(ROOT, "config", "config.json")
LOG = os.path.join(ROOT, "data", "sumrax.log")


def run_sumrax(*args):
    return subprocess.run(
        [SUMRAX] + list(args),
        cwd=ROOT,
        stdout=subprocess.PIPE,
        stderr=subprocess.PIPE,
        universal_newlines=True,
        env=os.environ.copy(),
    )


def chmod_bins():
    for p in (
        SUMRAX,
        os.path.join(ROOT, "scripts", "proxy_set_osx.sh"),
        os.path.join(ROOT, "core", "xray", "xray"),
        os.path.join(ROOT, "bin", "xray", "xray"),
    ):
        if os.path.isfile(p):
            os.chmod(p, 0o755)
    for xray in (
        os.path.join(ROOT, "core", "xray", "xray"),
        os.path.join(ROOT, "bin", "xray", "xray"),
    ):
        if os.path.isfile(xray):
            subprocess.run(["xattr", "-cr", xray], stderr=subprocess.DEVNULL)
            break


def is_running():
    r = run_sumrax("status")
    return "Running" in (r.stdout or "")


class App(tk.Tk):
    def __init__(self):
        super().__init__()
        self.title("Sumrak VPN")
        self.geometry("380x480")
        self.resizable(False, False)
        self.configure(bg=BG)

        title_f = tkfont.Font(size=22, weight="bold")
        btn_f = tkfont.Font(size=13, weight="bold")
        small_f = tkfont.Font(size=11)

        tk.Label(self, text="Sumrak VPN", font=title_f, fg=ACCENT, bg=BG).pack(pady=(24, 4))
        tk.Label(self, text="Proxy Utility · Xray", font=small_f, fg=MUTED, bg=BG).pack()

        self.status_dot = tk.Label(self, text="●", font=tkfont.Font(size=36), fg=MUTED, bg=BG)
        self.status_dot.pack(pady=(20, 0))
        self.status_lbl = tk.Label(self, text="Отключено", font=btn_f, fg=TEXT, bg=BG)
        self.status_lbl.pack(pady=(0, 8))

        frame = tk.Frame(self, bg=PANEL, padx=20, pady=20)
        frame.pack(padx=24, pady=12, fill="x")

        self.connect_btn = tk.Button(
            frame,
            text="ПОДКЛЮЧИТЬ",
            font=btn_f,
            fg="white",
            bg=GREEN,
            activebackground="#27ae60",
            activeforeground="white",
            relief="flat",
            padx=16,
            pady=14,
            cursor="hand2",
            command=self.toggle,
        )
        self.connect_btn.pack(fill="x")

        tk.Label(
            frame,
            text="Системный прокси · HTTP 10809 · SOCKS 10808",
            font=small_f,
            fg=MUTED,
            bg=PANEL,
        ).pack(pady=(12, 0))

        info = tk.Frame(self, bg=BG)
        info.pack(padx=24, fill="x")
        cfg_ok = os.path.isfile(CONFIG)
        tk.Label(
            info,
            text="config.json: " + ("готов" if cfg_ok else "нет — импортируй с Windows"),
            font=small_f,
            fg=GREEN if cfg_ok else RED,
            bg=BG,
            anchor="w",
        ).pack(fill="x")

        self.log = scrolledtext.ScrolledText(
            self, height=6, width=42, bg=PANEL, fg=TEXT, insertbackground=TEXT, font=small_f
        )
        self.log.pack(padx=24, pady=(8, 16), fill="both")
        self.log.configure(state="disabled")

        chmod_bins()
        self.refresh_ui()
        self.after(2500, self.tick)

    def log_line(self, text):
        self.log.configure(state="normal")
        self.log.insert(tk.END, text.rstrip() + "\n")
        self.log.see(tk.END)
        self.log.configure(state="disabled")

    def refresh_ui(self):
        on = is_running()
        if on:
            self.status_dot.config(fg=GREEN)
            self.status_lbl.config(text="Подключено")
            self.connect_btn.config(text="ОТКЛЮЧИТЬ", bg=RED, activebackground="#c0392b")
        else:
            self.status_dot.config(fg=MUTED)
            self.status_lbl.config(text="Отключено")
            self.connect_btn.config(text="ПОДКЛЮЧИТЬ", bg=GREEN, activebackground="#27ae60")

    def tick(self):
        self.refresh_ui()
        self.after(2500, self.tick)

    def toggle(self):
        if is_running():
            r = run_sumrax("stop")
            self.log_line((r.stdout or r.stderr or "stop").strip())
        else:
            if not os.path.isfile(CONFIG):
                messagebox.showwarning(
                    "Нет config.json",
                    "Положи config.json в:\n" + CONFIG,
                )
                return
            r = run_sumrax("start")
            out = ((r.stdout or "") + (r.stderr or "")).strip()
            self.log_line(out or "start")
            if r.returncode != 0 and os.path.isfile(LOG):
                with open(LOG, encoding="utf-8", errors="replace") as f:
                    self.log_line(f.read()[-1200:])
                messagebox.showerror("Ошибка", "Xray не запустился. Смотри лог.")
        self.refresh_ui()


if __name__ == "__main__":
    if sys.platform != "darwin":
        print("macOS only")
        sys.exit(1)
    App().mainloop()
