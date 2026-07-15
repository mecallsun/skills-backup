import pyodbc, sys
CONN = ('DRIVER={SQL Server};SERVER=192.168.1.237;DATABASE=WaterMeterDB;'
        'UID=__DB_USER__;PWD=__DB_PASSWORD__;TrustServerCertificate=yes;')
try:
    conn = pyodbc.connect(CONN, timeout=15)
except Exception as e:
    print("CONN_FAIL:", e); sys.exit(1)
cur = conn.cursor()
def cols(t):
    cur.execute("SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME=?", t)
    return [r[0] for r in cur.fetchall()]
def exists(t):
    cur.execute("SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME=?", t)
    return cur.fetchone()[0] > 0
for t in ['DormBooking','MeterRecord','BillingStandard','Team','Dorm']:
    if exists(t):
        print(f"[{t}] EXISTS cols=", cols(t))
    else:
        print(f"[{t}] *** MISSING TABLE ***")
conn.close()
