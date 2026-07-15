import pyodbc
CONN=('DRIVER={SQL Server};SERVER=192.168.1.237;DATABASE=WaterMeterDB;UID=__DB_USER__;PWD=__DB_PASSWORD__;TrustServerCertificate=yes;')
c=pyodbc.connect(CONN,timeout=15).cursor()
for t in ['DormBooking','MeterRecord']:
    c.execute("SELECT COLUMN_NAME,DATA_TYPE FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME=? AND COLUMN_NAME IN ('Status','BookingType','Type')",t)
    print(t, [(r[0],r[1]) for r in c.fetchall()])
