# WinLock2FA

Prosta aplikacja dla Windows, która działa jak dodatkowy, "drugi czynnik"
logowania na Twoim własnym komputerze: definiujesz zestaw pytań i
odpowiedzi bezpieczeństwa, a po każdym zalogowaniu do Windows na ekranie
pojawia się pełnoekranowa blokada z **losowo wybranym** pytaniem. Dopóki nie
podasz poprawnej odpowiedzi, blokada nie znika.

Zero zależności do uruchomienia: gotowy `WinLock2FA.exe` jest
self-contained (zawiera w sobie .NET) i działa na czystym Windows 10/11
x64 bez instalowania czegokolwiek dodatkowego.

## Jak to działa

- **Ustawienia (`Setup`)** — zwykłe okno, w którym dodajesz pytania i
  odpowiedzi. Odpowiedzi **nie** są nigdzie zapisywane jawnym tekstem: każda
  jest solona i hashowana SHA-256, a cały plik dodatkowo szyfrowany
  Windows DPAPI (`CurrentUser`), więc odczyta go tylko to samo konto
  Windows na tym samym komputerze.
- **Instalacja (`Install`)** — rejestruje zadanie w Harmonogramie zadań
  Windows (`schtasks`), które uruchamia `WinLock2FA.exe lock` przy każdym
  Twoim zalogowaniu. Zadanie działa z Twoimi normalnymi (nie-admin)
  uprawnieniami — instalacja nie wymaga uruchamiania niczego jako
  administrator i niczego nie zmienia w ustawieniach systemowych.
- **Blokada (`lock`)** — pełnoekranowe okno bez ramki, zawsze na wierzchu,
  bez paska zadań, z losowym pytaniem z Twojej puli. Blokuje Win, Alt+Tab,
  Alt+F4 i Ctrl+Esc, żeby zniechęcić do przełączenia się gdzie indziej. Po
  5 błędnych odpowiedziach wprowadzana jest 30-sekundowa blokada przed
  kolejną próbą.
- **Kod awaryjny (debug)** — wpisanie `0000` w polu odpowiedzi (nawet w
  trakcie 30-sekundowej blokady) natychmiast zamyka okno blokady, bez
  podawania prawdziwej odpowiedzi. To wygodne wyjście na czas testów, **nie**
  jest to sekret — kod jest jawnie w kodzie źródłowym w tym publicznym repo
  (`LockForm.cs`, stała `DebugOverrideCode`). Jeśli chcesz się na nim opierać
  jako na czymś więcej niż wygodą deweloperską, zmień go na coś swojego przed
  zbudowaniem `.exe`.

## Ważne ograniczenia bezpieczeństwa — przeczytaj przed użyciem

To **nie** jest zamiennik ekranu logowania Windows ani prawdziwa bariera
kryptograficzna. To dodatkowe, "miękkie" przypomnienie/utrudnienie na
poziomie aplikacji. Świadomie **nie** ingeruje w:

- **Ctrl+Alt+Del** — to systemowa "Secure Attention Sequence" obsługiwana
  bezpośrednio przez jądro Windows/Winlogon. Żadna aplikacja w trybie
  użytkownika nie jest w stanie jej przechwycić ani zablokować. Jest to
  celowo pozostawione jako **awaryjne wyjście**: w razie problemów
  `Ctrl+Alt+Del` → Menedżer zadań → zakończ zadanie `WinLock2FA`.
- Politykę systemową, rejestr Winlogon/Shell ani Menedżera zadań — appka
  nigdy tego nie modyfikuje.
- Tryb awaryjny (Safe Mode) — ktoś z fizycznym dostępem do komputera i
  kontem administratora zawsze może wyłączyć zaplanowane zadanie albo
  odinstalować program.

Innymi słowy: to warstwa "przypomnij mi / spowolnij kogoś", a nie
prawdziwe zabezpieczenie przed zdeterminowanym atakującym z fizycznym
dostępem. Do faktycznej ochrony konta i dysku używaj normalnego hasła
Windows, Windows Hello i BitLockera.

## Budowanie

Wymaga [.NET 8 SDK](https://dotnet.microsoft.com/) **tylko na etapie
budowania**. Wynikowy `.exe` nie potrzebuje niczego na maszynie docelowej.

```powershell
./build.ps1
```

Gotowy plik pojawi się w `dist\WinLock2FA.exe`.

## Użycie

1. Uruchom `WinLock2FA.exe` (bez argumentów) — otworzy się menu.
2. **"Ustaw pytania i odpowiedzi"** — dodaj min. 3 pytania.
3. **"Testuj blokadę teraz"** — sprawdź, czy pamiętasz odpowiedzi, zanim
   włączysz blokadę na stałe (aplikacja przypomni o Ctrl+Alt+Del przed
   testem).
4. **"Zainstaluj"** — od teraz blokada pojawi się po każdym zalogowaniu.
5. **"Odinstaluj"** — usuwa zaplanowane zadanie, blokada przestaje się
   uruchamiać.

Dane pytań/odpowiedzi trzymane są w
`%APPDATA%\WinLock2FA\questions.dat`.

## Licencja

MIT — zobacz [LICENSE](LICENSE).
