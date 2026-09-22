# Instrukcja wdrozenia w Urzedzie Gminy

Dokument przeznaczony dla informatyka odpowiedzialnego za wdrozenie 
systemu Asystent Gminy w jednostce samorzadu terytorialnego.

## 📋 Wymagania sprzetowe

### Serwer (rekomendowany dla produkcji)

| Komponent | Minimum | Zalecane |
|---|---|---|
| CPU | 8 rdzeni | 16 rdzeni |
| RAM | 16 GB | 32 GB |
| Dysk | 100 GB SSD | 500 GB SSD |
| GPU | brak (CPU inference) | NVIDIA RTX 4090 / A6000 (24 GB VRAM) |
| System | Windows Server 2022 / Ubuntu 22.04 LTS | jak obok |

### Komputer uzytkownika (aplikacja desktop)

| Komponent | Minimum |
|---|---|
| System | Windows 10/11 |
| RAM | 4 GB |
| Dysk | 200 MB |
| Siec | Dostep do API (LAN) |

## 🏗️ Architektura wdrozenia

W wersji produkcyjnej zalecany jest podzial na:

- Serwer aplikacyjny (API + PostgreSQL + Ollama) – 1 maszyna
- Komputery uzytkownikow (aplikacja desktop) – wiele maszyn

Wszystkie maszyny w sieci wewnetrznej (LAN). Brak dostepu z internetu.

## 🔧 Krok 1: Przygotowanie serwera

### Windows Server

1. Zainstaluj .NET 10 SDK (Runtime + ASP.NET Core Runtime).
2. Zainstaluj PostgreSQL 18 z rozszerzeniem pgvector.
3. Zainstaluj Docker Desktop (alternatywnie).
4. Zainstaluj Ollama.
5. Zainstaluj Git (opcjonalnie).

### Ubuntu

    sudo apt update
    sudo apt install -y dotnet-sdk-10.0 postgresql-18 postgresql-18-pgvector
    
    # Ollama
    curl -fsSL https://ollama.com/install.sh | sh

## 🔧 Krok 2: Konfiguracja bazy danych

    sudo -u postgres psql
    
    CREATE DATABASE asystent_gminy;
    \c asystent_gminy
    CREATE EXTENSION vector;
    \q

Utworz dedykowanego uzytkownika (zamiast postgres):

    CREATE USER asystent WITH PASSWORD 'silne_haslo';
    GRANT ALL PRIVILEGES ON DATABASE asystent_gminy TO asystent;
    GRANT ALL ON SCHEMA public TO asystent;

## 🔧 Krok 3: Pobranie modeli AI

    ollama pull nomic-embed-text-v2-moe
    ollama pull SpeakLeash/bielik-11b-v3.0-instruct:Q4_K_M

Na serwerze z GPU: sprawdz, czy Ollama uzywa GPU:

    ollama ps

Kolumna PROCESSOR powinna zawierac "GPU" (a nie "100% CPU").

## 🔧 Krok 4: Wgranie projektu

    git clone https://github.com/twoje-repo/AsystentGminy.git
    cd AsystentGminy

Zaktualizuj connection string w:

- AsystentGminy.Ingestion/Data/AppDbContext.cs
- AsystentGminy.Api/Data/AppDbContext.cs
- AsystentGminy.Api/appsettings.json

## 🔧 Krok 5: Ingestion dokumentow

Umiesc dokumenty PDF w katalogu knowledge_base/.

    cd AsystentGminy.Ingestion
    dotnet run

Proces moze potrwac od kilku minut do kilku godzin w zaleznosci od 
liczby dokumentow i uzytego OCR.

## 🔧 Krok 6: Uruchomienie API

W wersji produkcyjnej uruchom jako usluga systemowa:

### Windows (jako usluga)

Uzyj NSSM (Non-Sucking Service Manager):

    nssm install AsystentGminyApi "C:\Program Files\dotnet\dotnet.exe" "run --project C:\AsystentGminy\AsystentGminy.Api"

### Ubuntu (systemd)

Utworz plik /etc/systemd/system/asystent-api.service:

    [Unit]
    Description=Asystent Gminy API
    After=network.target postgresql.service
    
    [Service]
    WorkingDirectory=/opt/AsystentGminy/AsystentGminy.Api
    ExecStart=/usr/bin/dotnet run
    Restart=always
    User=asystent
    
    [Install]
    WantedBy=multi-user.target

Aktywuj usluge:

    sudo systemctl enable asystent-api
    sudo systemctl start asystent-api

## 🔧 Krok 7: Konfiguracja HTTPS

W produkcji API musi dzialac po HTTPS. W appsettings.json:

    "Kestrel": {
      "Endpoints": {
        "Https": {
          "Url": "https://0.0.0.0:5001",
          "Certificate": {
            "Path": "/etc/ssl/certs/asystent.pfx",
            "Password": "haslo_certyfikatu"
          }
        }
      }
    }

## 🔧 Krok 8: Aplikacja desktop dla pracownikow

Zbuduj wersje release:

    cd AsystentGminy.Desktop
    dotnet publish -c Release -r win-x64 --self-contained

Wynik w bin/Release/net10.0/win-x64/publish/.

Rozdystrybuuj folder pracownikom. Uruchamiaja plik AsystentGminy.exe.

## 🔧 Krok 9: Konfiguracja firewall

Otworz port API (np. 5001) w sieci wewnetrznej:

    New-NetFirewallRule -DisplayName "Asystent Gminy API" -Direction Inbound -LocalPort 5001 -Protocol TCP -Action Allow

## 🔧 Krok 10: Test koncowy

1. Uruchom przegladarke na komputerze pracownika.
2. Wejdz na https://serwer:5001/api/health – powinno zwrocic {"status":"ok"}.
3. Uruchom aplikacje desktop i zadasz pytanie testowe.
4. Sprawdz, czy odpowiedz zawiera cytowania zrodel.

## 📊 Monitoring i utrzymanie

### Codzienne
- Sprawdz, czy usluga API dziala.
- Sprawdz, czy Ollama odpowiada (curl http://localhost:11434/api/tags).

### Tygodniowe
- Backup bazy: pg_dump asystent_gminy > backup.sql
- Sprawdz logi API pod katem bledow.

### Miesieczne
- Dodaj nowe dokumenty do knowledge_base i uruchom ingestion.
- Sprawdz aktualizacje modeli Bielik.
- Przejrzyj liste zaindeksowanych dokumentow pod katem RODO.

## 🚨 Rozwiazywanie problemow

| Problem | Rozwiazanie |
|---|---|
| API nie startuje | Sprawdz logi, czy baza dziala |
| Ollama odpowiada wolno | Sprawdz, czy uzywa GPU: ollama ps |
| Blad polaczenia z bazy | Sprawdz connection string i firewall |
| Aplikacja desktop nie laczy sie z API | Sprawdz port i certyfikat HTTPS |
| OCR bardzo wolny | Rozwaz dodanie GPU lub uzycie modelu w chmurze (zgodnie z RODO) |
