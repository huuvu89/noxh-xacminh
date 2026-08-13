@echo off
REM Cong cu la WebAssembly: trinh duyet tai phan chay bang fetch, ma fetch tren file:// bi chan --
REM nen ban offline van can mot may chu file, chay ngay tren may nay. Khong co goi tin nao ra Internet.
setlocal
if "%CONG%"=="" set CONG=8080

echo Mo http://localhost:%CONG%/ trong trinh duyet. Ctrl+C de dung.
python -m http.server %CONG% --directory "%~dp0"
if errorlevel 1 (
    echo.
    echo Khong chay duoc python. Cai Python 3, hoac dung mot may chu file tinh bat ky
    echo tren chinh thu muc nay ^(xem HUONG-DAN-OFFLINE.md^).
    pause
)
