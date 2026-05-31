@echo off
start "Backend .NET" cmd /k "cd /d D:\recouvrement_client-main\recouvrement_client-main\backend_new && dotnet run"
start "FastAPI Scoring" cmd /k "cd /d D:\recouvrement_client-main\recouvrement_client-main\stb-scoring-api && C:\Users\donye\AppData\Local\Python\bin\python.exe -m uvicorn main:app --host 0.0.0.0 --port 8000"