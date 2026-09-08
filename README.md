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
  bez paska zadań, z losowym pytaniem z Twojej puli. Pole odpowiedzi jest
  zamaskowane (jak pole hasła). Blokuje Win, Alt+Tab, Alt+F4 i Ctrl+Esc,
  żeby zniechęcić do przełączenia się gdzie indziej, i nie wyświetla żadnej
  podpowiedzi o Ctrl+Alt+Del ani Menedżerze zadań na ekranie. Po 5 błędnych
  odpowiedziach wprowadzana jest 30-sekundowa blokada przed kolejną próbą.
- **Kod awaryjny (debug)** — wpisanie `0000` w polu odpowiedzi (nawet w
  trakcie 30-sekundowej blokady) natychmiast zamyka okno blokady, bez
  podawania prawdziwej odpowiedzi. Ten sam kod jest też wymagany w menu
  głównym przy akcji **"Odinstaluj"**, żeby całkowicie wyłączyć ochronę —
  bez niego nie da się wyłączyć programu z poziomu jego własnego interfejsu
  (poza usunięciem zadania ręcznie z uprawnieniami administratora, patrz
  niżej). To wygodne wyjście na czas testów, **nie** jest to sekret — kod
  jest jawnie w kodzie źródłowym w tym publicznym repo (`DebugCode.cs`,
  stała `DebugCode.Value`). Jeśli chcesz się na nim opierać jako na czymś
  więcej niż wygodą deweloperską, zmień go na coś swojego przed zbudowaniem
  `.exe`.

## Ważne ograniczenia bezpieczeństwa — przeczytaj przed użyciem

To **nie** jest zamiennik ekranu logowania Windows ani prawdziwa bariera
kryptograficzna. To dodatkowe, "miękkie" przypomnienie/utrudnienie na
poziomie aplikacji. Świadomie **nie** ingeruje w:

- **Ctrl+Alt+Del** — to systemowa "Secure Attention Sequence" obsługiwana
  bezpośrednio przez jądro Windows/Winlogon. Żadna aplikacja w trybie
  użytkownika nie jest w stanie jej przechwycić ani zablokować. To celowo
  pozostawione, zawsze działające awaryjne wyjście — `Ctrl+Alt+Del` →
  Menedżer zadań → zakończ zadanie `WinLock2FA` — nawet jeśli ekran
  blokady nie wyświetla już o tym podpowiedzi.
- **Menedżer zadań pozostaje w pełni dostępny.** Świadomie **nie**
  implementuję jego wyłączania (np. przez politykę rejestru
  `DisableTaskMgr`) ani żadnej innej modyfikacji ustawień systemowych —
  to wykracza poza to, co ta appka powinna robić: zmiana takich ustawień
  bezpieczeństwa systemu Windows niesie realne ryzyko, że w razie błędu w
  aplikacji zablokujesz się na własnym, żywym komputerze bez żadnej drogi
  odzyskania dostępu (poza Trybem awaryjnym i kontem administratora). Appka
  nigdy nie modyfikuje polityki systemowej ani rejestru Winlogon/Shell.
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
5. **"Odinstaluj"** — poprosi o kod awaryjny (`0000`), a po jego podaniu
   usuwa zaplanowane zadanie i blokada przestaje się uruchamiać.

Dane pytań/odpowiedzi trzymane są w
`%APPDATA%\WinLock2FA\questions.dat`.

## Licencja

MIT — zobacz [LICENSE](LICENSE).
