import pyodbc
c=pyodbc.connect('DRIVER={SQL Server};SERVER=192.168.1.237;DATABASE=WaterMeterDB;UID=__DB_USER__;PWD=__DB_PASSWORD__;TrustServerCertificate=yes;',timeout=15).cursor()
c.execute("SELECT TOP 1 DormId FROM Dorm ORDER BY DormId"); print("DormId=",c.fetchone()[0])
c.execute("SELECT TOP 3 Id FROM BillingStandard ORDER BY Id"); print("BillingIds=",[r[0] for r in c.fetchall()])
