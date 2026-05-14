import threading
import schedule
import time
import os
import sys
import logging
import tkinter as tk
from tkinter import ttk, messagebox
from PIL import Image, ImageDraw
import pystray
from dbfread import DBF
import pandas as pd
import psycopg2
from psycopg2.extras import execute_values
from datetime import datetime
import json

# ===== Config file อยู่ข้างๆ .exe =====
CONFIG_FILE = os.path.join(os.path.dirname(sys.executable 
              if getattr(sys, 'frozen', False) else __file__), "config.json")
LOG_FILE    = os.path.join(os.path.dirname(sys.executable 
              if getattr(sys, 'frozen', False) else __file__), "etl_log.txt")

logging.basicConfig(filename=LOG_FILE, level=logging.INFO,
                    format="%(asctime)s %(levelname)s %(message)s")

DEFAULT_CONFIG = {
    "dbf_path": r"C:\Program Files (x86)\ExpressD\dat",
    "pg_host": "",
    "pg_port": "5432",
    "pg_db": "",
    "pg_user": "",
    "pg_pass": "",
    "interval_hours": 1
}

def load_config():
    if os.path.exists(CONFIG_FILE):
        with open(CONFIG_FILE) as f:
            return json.load(f)
    return DEFAULT_CONFIG.copy()

def save_config(cfg):
    with open(CONFIG_FILE, "w") as f:
        json.dump(cfg, f, indent=2)

# ===== ETL Logic =====
def get_pg(cfg):
    return psycopg2.connect(
        host=cfg["pg_host"], port=cfg["pg_port"],
        dbname=cfg["pg_db"], user=cfg["pg_user"], password=cfg["pg_pass"]
    )

def read_dbf(cfg, filename):
    path = os.path.join(cfg["dbf_path"], filename)
    table = DBF(path, encoding="tis-620", ignore_missing_memofile=True)
    return pd.DataFrame(iter(table))

def upsert(conn, table, rows, conflict_col):
    if not rows: return
    cols = list(rows[0].keys())
    values = [tuple(r[c] for c in cols) for r in rows]
    update_cols = [c for c in cols if c != conflict_col]
    update_str = ", ".join(f"{c}=EXCLUDED.{c}" for c in update_cols)
    sql = f"""
        INSERT INTO {table} ({','.join(cols)}) VALUES %s
        ON CONFLICT ({conflict_col}) DO UPDATE SET {update_str}
    """
    with conn.cursor() as cur:
        execute_values(cur, sql, values)
    conn.commit()

def run_etl(cfg, status_callback=None):
    def notify(msg):
        logging.info(msg)
        if status_callback: status_callback(msg)

    notify("ETL Started...")
    try:
        conn = get_pg(cfg)

        # Customers
        df = read_dbf(cfg, "ARMAS.DBF")
        rows = [{"express_code": str(r.get("CUSTCODE","")).strip(),
                 "name": str(r.get("CUSTNAME","")).strip(),
                 "tax_id": str(r.get("TAXID","")).strip(),
                 "updated_at": datetime.now()}
                for _, r in df.iterrows() if str(r.get("CUSTCODE","")).strip()]
        upsert(conn, "express_staging.customers", rows, "express_code")
        notify(f"✓ Customers: {len(rows)} rows")

        # Suppliers
        df = read_dbf(cfg, "APMAS.DBF")
        rows = [{"express_code": str(r.get("VENDCODE","")).strip(),
                 "name": str(r.get("VENDNAME","")).strip(),
                 "tax_id": str(r.get("TAXID","")).strip(),
                 "updated_at": datetime.now()}
                for _, r in df.iterrows() if str(r.get("VENDCODE","")).strip()]
        upsert(conn, "express_staging.suppliers", rows, "express_code")
        notify(f"✓ Suppliers: {len(rows)} rows")

        # Items
        df = read_dbf(cfg, "ICMAS.DBF")
        rows = [{"item_code": str(r.get("ITEMCODE","")).strip(),
                 "item_name": str(r.get("ITEMNAME","")).strip(),
                 "sale_price": float(r.get("SALEPRICE", 0) or 0),
                 "updated_at": datetime.now()}
                for _, r in df.iterrows() if str(r.get("ITEMCODE","")).strip()]
        upsert(conn, "express_staging.items", rows, "item_code")
        notify(f"✓ Items: {len(rows)} rows")

        conn.close()
        notify("ETL Completed ✓")
    except Exception as e:
        logging.error(f"ETL Error: {e}", exc_info=True)
        notify(f"✗ Error: {e}")

# ===== Settings Window =====
class SettingsWindow:
    def __init__(self, root, cfg, on_save):
        self.win = tk.Toplevel(root)
        self.win.title("Express → PostgreSQL Settings")
        self.win.geometry("420x380")
        self.win.resizable(False, False)

        fields = [
            ("DBF Path",    "dbf_path"),
            ("PG Host",     "pg_host"),
            ("PG Port",     "pg_port"),
            ("PG Database", "pg_db"),
            ("PG User",     "pg_user"),
            ("PG Password", "pg_pass"),
        ]
        self.vars = {}
        for i, (label, key) in enumerate(fields):
            ttk.Label(self.win, text=label).grid(row=i, column=0, padx=12, pady=6, sticky="w")
            var = tk.StringVar(value=cfg.get(key, ""))
            show = "*" if key == "pg_pass" else ""
            entry = ttk.Entry(self.win, textvariable=var, width=32, show=show)
            entry.grid(row=i, column=1, padx=12, pady=6)
            self.vars[key] = var

        # Status label
        self.status = ttk.Label(self.win, text="", foreground="gray")
        self.status.grid(row=len(fields)+1, column=0, columnspan=2, pady=4)

        btn_frame = ttk.Frame(self.win)
        btn_frame.grid(row=len(fields)+2, column=0, columnspan=2, pady=10)

        ttk.Button(btn_frame, text="Test Connection", 
                   command=self.test_conn).pack(side="left", padx=6)
        ttk.Button(btn_frame, text="Save & Close",
                   command=lambda: self.save(on_save)).pack(side="left", padx=6)

    def get_cfg(self):
        return {k: v.get() for k, v in self.vars.items()}

    def test_conn(self):
        cfg = self.get_cfg()
        try:
            conn = get_pg(cfg)
            conn.close()
            self.status.config(text="✓ Connection OK", foreground="green")
        except Exception as e:
            self.status.config(text=f"✗ {e}", foreground="red")

    def save(self, on_save):
        cfg = self.get_cfg()
        save_config(cfg)
        on_save(cfg)
        self.win.destroy()

# ===== Main App =====
class App:
    def __init__(self):
        self.cfg = load_config()
        self.root = tk.Tk()
        self.root.withdraw()  # ซ่อน main window
        self.tray = None
        self._start_tray()
        self._schedule_etl()
        self.root.mainloop()

    def _make_icon(self, color="green"):
        img = Image.new("RGB", (64, 64), color=color)
        d = ImageDraw.Draw(img)
        d.ellipse([8, 8, 56, 56], fill="white")
        d.text((20, 20), "ETL", fill=color)
        return img

    def _start_tray(self):
        menu = pystray.Menu(
            pystray.MenuItem("Run Now", self._run_now),
            pystray.MenuItem("Settings", self._open_settings),
            pystray.MenuItem("View Log", self._view_log),
            pystray.MenuItem("Exit", self._exit)
        )
        self.tray = pystray.Icon("ExpressETL", self._make_icon(), 
                                  "Express ETL — Running", menu)
        threading.Thread(target=self.tray.run, daemon=True).start()

    def _schedule_etl(self):
        hours = int(self.cfg.get("interval_hours", 1))
        schedule.clear()
        schedule.every(hours).hours.do(self._run_etl_thread)
        threading.Thread(target=self._scheduler_loop, daemon=True).start()

    def _scheduler_loop(self):
        while True:
            schedule.run_pending()
            time.sleep(30)

    def _run_now(self):
        self._run_etl_thread()

    def _run_etl_thread(self):
        threading.Thread(target=run_etl, args=(self.cfg,), daemon=True).start()

    def _open_settings(self):
        self.root.after(0, lambda: SettingsWindow(
            self.root, self.cfg,
            on_save=lambda new_cfg: self._on_config_saved(new_cfg)
        ))

    def _on_config_saved(self, new_cfg):
        self.cfg = new_cfg
        self._schedule_etl()

    def _view_log(self):
        os.startfile(LOG_FILE)

    def _exit(self):
        self.tray.stop()
        self.root.quit()

if __name__ == "__main__":
    App()
