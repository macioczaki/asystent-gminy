# Bezpieczenstwo i zgodnosc z RODO

## 🔒 Zasada dzialania

Asystent Gminy dziala w 100% lokalnie. Zadne dane nie sa wysylane 
do zewnetrznych serwerow, chmur AI ani dostawcow trzecich.

Wszystkie komponenty (model LLM, embeddingi, baza wektorowa, API, UI) 
dzialaja na serwerach lub komputerach nalezacych do Urzedu Gminy.

## 📋 Zgodnosc z RODO

### Art. 5 – Zasady przetwarzania
- Minimalizacja – asystent ma dostep wylacznie do dokumentow 
  umieszczonych w bazie wiedzy przez administratora.
- Ograniczenie celu – dane sa przetwarzane wylacznie w celu 
  ulatwienia pracy pracownikom urzedu.

### Art. 25 – Privacy by design
- Model jezykowy dziala lokalnie – brak transferu danych do chmury.
- Brak zewnetrznych API (OpenAI, Azure, Google) – wszystko on-premise.
- Logi rozmow moga byc wylaczone lub przechowywane lokalnie.

### Art. 32 – Bezpieczenstwo przetwarzania
- Szyfrowanie w tranzycie – HTTPS miedzy UI a API (do wlaczenia w produkcji).
- Szyfrowanie w spoczynku – PostgreSQL obsluguje TDE lub LUKS na poziomie dysku.
- Kontrola dostepu – baza i API wymagaja uwierzytelnienia (do wdrozenia).
- Rozdzielenie srodowisk – development i produkcja na osobnych maszynach.

## 👤 Zasada "human in the loop"

Asystent wspiera pracownika, ale nie podejmuje decyzji:
- Nie wystawia decyzji administracyjnych.
- Nie interpretuje przepisow w sposob wiazacy.
- Nie kontaktuje sie z interesariuszami.
- Odpowiedzi sa sugestiami – pracownik weryfikuje je przed uzyciem.

## 📚 Zalecenia dla wdrozenia

### Przed uruchomieniem produkcyjnym:
1. Zgoda Inspektora Ochrony Danych (IOD) – konsultacja przed wdrozeniem.
2. Rejestr czynnosci przetwarzania – wpisanie systemu jako narzedzia.
3. Ocena skutkow dla ochrony danych (DPIA) – jesli system 
   przetwarza dane osobowe (np. z rejestru mieszkancow).
4. Umowa powierzenia przetwarzania – jesli serwer hostowany jest poza urzedem.
5. Uwierzytelnianie – logowanie pracownikow (Active Directory / LDAP).
6. HTTPS – certyfikat SSL na API.
7. Backupy – regularne kopie bazy wektorowej i dokumentow.

### Zalecenia operacyjne:
- Nie umieszczaj w bazie wiedzy dokumentow zawierajacych dane 
  osobowe (imiona, PESEL, adresy) – chyba ze zostana zanonimizowane.
- Regularnie audytuj baze – raz w miesiacu przejrzyj liste 
  zaindeksowanych dokumentow.
- Aktualizuj model – gdy pojawia sie nowsze wersje Bielika, 
  testuj je w srodowisku deweloperskim.
- Monitoruj logi – wyrywkowo sprawdzaj, jakie pytania zadaja pracownicy.

## 🚨 Ograniczenia systemu

- Model moze sie mylic – Bielik 4.5B ma halucynacje. W produkcji 
  zalecany model 11B (lepsza jakosc).
- Brak pamieci konwersacji – kazde pytanie jest niezalezne.
- Brak kontroli dostepu – obecnie kazdy, kto ma dostep do API, 
  moze zadawac pytania. Do wdrozenia: logowanie.
- Brak audytu – nie logujemy, kto zadal pytanie i kiedy. 
  Do wdrozenia: rejestr zdarzen.

## 🔐 Bezpieczenstwo techniczne

| Warstwa | Ryzyko | Mitygacja |
|---|---|---|
| LLM | Prompt injection | Walidacja wejscia, brak wykonywania kodu |
| Baza | Nieautoryzowany dostep | Haslo, firewall, VPN |
| API | DDoS, spam | Rate limiting (do wdrozenia) |
| UI | Kradziez komputera | Szyfrowanie dysku (BitLocker) |
| Dokumenty | Wyciek danych | Kontrola dostepu do knowledge_base/ |

## 📞 Kontakt

W sprawach bezpieczenstwa: macioczaki@gmail.com
