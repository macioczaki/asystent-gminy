# Asystent Gminy
![.NET](https://img.shields.io/badge/.NET-10.0_LTS-512BD4?logo=dotnet&logoColor=white)
![Avalonia](https://img.shields.io/badge/Avalonia-12.1-8B44AC?logo=avalonia&logoColor=white)
![PostgreSQL](https://img.shields.io/badge/PostgreSQL-18-4169E1?logo=postgresql&logoColor=white)
![pgvector](https://img.shields.io/badge/pgvector-0.8.6-4169E1)
![Ollama](https://img.shields.io/badge/Ollama-Bielik_4.5B-000000?logo=ollama&logoColor=white)
![License](https://img.shields.io/badge/license-Wewn%C4%99trzna-lightgrey)
![Platform](https://img.shields.io/badge/platform-Windows%20%7C%20Linux%20%7C%20macOS-blue)

Lokalny asystent AI dla pracowników Urzędu Gminy. Odpowiada na pytania 
o dokumenty urzędowe (uchwały, regulaminy, ustawy) w języku naturalnym,
cytując źródła. **Wszystkie dane pozostają na serwerze urzędu** – żadne 
informacje nie są wysyłane do chmury.

## 📸 Zrzuty ekranu

### Główne okno – pytanie i odpowiedź z cytowaniem źródeł

![Główne okno Asystenta Gminy](docs/screenshots/main-window.png)

*Asystent odpowiada na pytanie o zadania wójta gminy, cytując konkretne fragmenty dokumentów źródłowych.*

### Panel administracyjny – zarządzanie bazą wiedzy

![Panel administracyjny z listą dokumentów](docs/screenshots/documents-panel.png)

*Panel administracyjny umożliwia przeglądanie i usuwanie dokumentów z bazy wiedzy.*

---

## 🎯 Dlaczego ten projekt?

Pracownicy urzędów gmin codziennie tracą godziny na wyszukiwanie informacji 
w dziesiątkach dokumentów – uchwałach, regulaminach, ustawach. Odpowiedź na proste 
pytanie typu *„Jakie zadania realizuje wójt gminy?"* wymaga przekopania się przez 
setki stron PDF-ów, często dostępnych wyłącznie jako **skany wymagające OCR**.

**Asystent Gminy** rozwiązuje ten problem:

- **Odpowiada w sekundach** zamiast minut – w języku naturalnym, po polsku.
- **Cytuje źródła** – każda informacja ma numer dokumentu, z którego pochodzi.
- **Działa lokalnie** – dane nie opuszczają serwera urzędu, zgodnie z RODO.
- **Obsługuje skany** – OCR (Tesseract) przetwarza dokumenty bez warstwy tekstowej.
- **Jest prosty w użyciu** – aplikacja desktopowa, bez konieczności szkoleń.

Projekt został wdrożony w Urzędzie Gminy [nazwa] i obsługuje [X] dokumentów 
oraz [Y] pracowników.

---

## ✨ Funkcje

- 💬 **Czat w języku naturalnym** – pytania po polsku, odpowiedzi po polsku
- 📚 **Odpowiedzi oparte na dokumentach** – model nie zmyśla, cytuje źródła
- 🔍 **Wyszukiwanie semantyczne** – rozumie sens pytania, nie tylko słowa
- 📄 **Obsługa PDF i skanów** – OCR (Tesseract) dla dokumentów bez warstwy tekstowej
- 🖥️ **Aplikacja desktopowa** – Avalonia 12, działa na Windows/Linux/macOS
- 🔒 **W 100% lokalnie** – Ollama + Bielik, brak połączenia z internetem
- 🌙 **Tryb ciemny/jasny** – automatyczne dopasowanie do systemu

---

## 🏗️ Architektura

System składa się z trzech niezależnych projektów .NET 10 oraz bazy danych z rozszerzeniem wektorowym:

    ┌─────────────────────────────────────────────────────────────────────────┐
    │  APLIKACJA DESKTOPOWA (Avalonia 12 / .NET 10)                          │
    │  ┌───────────────────────────────────────────────────────────────────┐  │
    │  │  Widoki (MVVM)  →  MainViewModel  →  ChatApiClient (HTTP)        │  │
    │  │  • Czat w języku naturalnym         • Eksport rozmowy do PDF      │  │
    │  │  • Panel administracyjny            • Upload dokumentów PDF       │  │
    │  │  • Historia rozmów                  • Streaming odpowiedzi (SSE)  │  │
    │  └───────────────────────────────────────────────────────────────────┘  │
    └─────────────────────────────────┬───────────────────────────────────────┘
                                      │ HTTP / SSE
    ┌─────────────────────────────────▼───────────────────────────────────────┐
    │  API (ASP.NET Core / .NET 10)                                          │
    │  ┌───────────────────────────────────────────────────────────────────┐  │
    │  │  Endpointy:                          Serwisy:                      │  │
    │  │  • POST /api/chat                    • ChatService (RAG)          │  │
    │  │  • POST /api/chat/stream             • SearchService (pgvector)   │  │
    │  │  • POST /api/documents/upload        • IngestionService (OCR)     │  │
    │  │  • GET  /api/documents               • OllamaEmbeddingService     │  │
    │  │  • GET  /api/conversations                                         │  │
    │  └───────────────────────────────────────────────────────────────────┘  │
    └──────────┬────────────────────────────────────────────┬────────────────┘
               │                                            │
               │ SQL                                        │ HTTP (localhost)
               ▼                                            ▼
    ┌──────────────────────────────────┐    ┌─────────────────────────────────┐
    │  PostgreSQL 18 + pgvector 0.8.6  │    │  Ollama                         │
    │  ┌────────────────────────────┐  │    │  • Bielik 4.5B (LLM)            │
    │  │ • documents (6 dok.)       │  │    │  • nomic-embed-text-v2-moe      │
    │  │ • chunks (733 wektory)     │  │    │    (embeddingi 768-wymiarowe)   │
    │  │ • conversations            │  │    └─────────────────────────────────┘
    │  │ • messages                 │  │
    │  └────────────────────────────┘  │
    └──────────────────────────────────┘

### Przepływ zapytania (RAG)

1. Użytkownik wpisuje pytanie w aplikacji desktopowej.
2. Aplikacja wysyła je do API (`POST /api/chat/stream`).
3. API generuje embedding pytania przez **nomic-embed-text-v2-moe**.
4. **pgvector** wyszukuje 2 najbliższe fragmenty dokumentów (podobieństwo kosinusowe).
5. API buduje prompt z kontekstem i wysyła go do **Bielika 4.5B** przez Ollamę.
6. Bielik generuje odpowiedź strumieniowo (słowo po słowie).
7. API zwraca fragmenty przez **SSE** do aplikacji desktopowej.
8. Aplikacja wyświetla odpowiedź oraz źródła z numerami `[1]`, `[2]`.

### Stack technologiczny

| Warstwa | Technologia | Wersja |
|---|---|---|
| **UI desktop** | Avalonia + CommunityToolkit.Mvvm | 12.1.2 |
| **API** | ASP.NET Core (Minimal API) + Scalar | .NET 10 |
| **Baza danych** | PostgreSQL + pgvector | 18.6 / 0.8.6 |
| **ORM** | Entity Framework Core + Pgvector.EntityFrameworkCore | 10.0 |
| **LLM** | Ollama + Bielik (SpeakLeash) | 4.5B Q4_K_M |
| **Embeddingi** | nomic-embed-text-v2-moe | – |
| **OCR** | Tesseract + PDFtoImage | 5.5.2 |
| **PDF** | PdfOxide / QuestPDF | – |

---

### Stack technologiczny

| Warstwa | Technologia | Wersja |
|---|---|---|
| UI desktop | Avalonia + CommunityToolkit.Mvvm | 12.1.2 |
| API | ASP.NET Core (Minimal API) + Scalar | .NET 10 |
| Baza danych | PostgreSQL + pgvector | 18.6 / 0.8.6 |
| ORM | Entity Framework Core + Pgvector.EntityFrameworkCore | 10.0 |
| LLM | Ollama + Bielik (SpeakLeash) | 4.5B / 11B |
| Embeddingi | nomic-embed-text-v2-moe | – |
| OCR | Tesseract + PDFtoImage | 5.5.2 |
| PDF | PdfOxide | – |

---

## 🚀 Szybki start

### Wymagania

- Windows 10/11, Linux lub macOS
- .NET 10 SDK
- Docker Desktop (dla PostgreSQL)
- Ollama
- 16 GB RAM (minimum), 32 GB zalecane
- Karta graficzna z 8+ GB VRAM (opcjonalnie – przyspiesza LLM)

### 1. Baza danych

Uruchom kontener PostgreSQL z pgvector:

    docker run -d --name pg-asystent --restart unless-stopped -e POSTGRES_PASSWORD=mojehaslo -p 5432:5432 pgvector/pgvector:pg18

Nastepnie utworz baze i aktywuj rozszerzenie w psql:

    CREATE DATABASE asystent_gminy;
    \c asystent_gminy
    CREATE EXTENSION vector;

### 2. Modele AI

    ollama pull nomic-embed-text-v2-moe
    ollama pull SpeakLeash/bielik-minitron-7B-v3.0-instruct:Q4_K_M

### 3. Ingestion – zaindeksuj dokumenty

Umiesc pliki PDF w katalogu knowledge_base, nastepnie uruchom:

    cd AsystentGminy.Ingestion
    dotnet run

### 4. Uruchom API

    cd AsystentGminy.Api
    dotnet run

Dokumentacja API dostepna pod adresem https://localhost:PORT/scalar

### 5. Uruchom aplikacje desktop

    cd AsystentGminy.Desktop
    dotnet run

---

## 📁 Struktura projektu

    AsystentGminy/
    ├── AsystentGminy.Desktop/    # Aplikacja desktop (Avalonia)
    ├── AsystentGminy.Api/        # Backend API (ASP.NET Core)
    ├── AsystentGminy.Ingestion/  # Indeksowanie dokumentow
    ├── knowledge_base/           # Dokumenty do zaindeksowania (PDF)
    ├── README.md                 # Ten plik
    ├── SECURITY.md               # Analiza bezpieczenstwa i RODO
    ├── INSTALL.md                # Instrukcja wdrozenia w urzedzie
    └── USER_GUIDE.md             # Instrukcja dla pracownikow

---

## 📖 Dokumentacja

- SECURITY.md – bezpieczenstwo i zgodnosc z RODO
- INSTALL.md – wdrozenie krok po kroku
- USER_GUIDE.md – instrukcja dla uzytkownikow

---

## 🎯 Status projektu

- ✅ Faza 1: Ingestion (PDF → wektory) – zakonczona
- ✅ Faza 2: API RAG (pytanie → odpowiedz) – zakonczona
- ✅ Faza 3: UI Avalonia – zakonczona
- 🔄 Faza 4: Wdrozenie w urzedzie – w toku

---

## 📝 Licencja

Projekt wewnetrzny – do uzytku w jednostkach samorzadu terytorialnego.

## 👤 Autor

Marcin Orłowski
