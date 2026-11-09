# LM Studio Trigger für Boray POWer

Ein kleines Windows-Tray-Programm, das **LM-Studio-Anfragen als Benutzeraktivität** an [Boray POWer](http://www.boray.se/software/boraypower/) meldet.

Boray POWer schaltet zwischen zwei Windows-Energieplänen um: **Höchstleistung**, sobald Maus oder Tastatur benutzt werden, und **Energiesparen** nach einer einstellbaren Leerlaufzeit. Auf einem PC, der 24/7 als LLM-Server läuft, reicht das nicht aus. Remote-Anfragen an LM Studio (aus LAN, VPN oder z. B. PocketPal) erzeugen keine Maus- oder Tastatureingaben, und der Rechner bleibt im Energiesparmodus.

**LM Studio Trigger** schließt diese Lücke. Boray POWer selbst wird dabei **nicht verändert**.

---

## Inhalt

- [Funktionsweise](#funktionsweise)
- [Voraussetzungen](#voraussetzungen)
- [Kompilieren](#kompilieren)
- [Installation und Autostart](#installation-und-autostart)
- [Bedienung](#bedienung)
- [Konfiguration](#konfiguration)
- [Zeitlicher Ablauf](#zeitlicher-ablauf)
- [Testen](#testen)
- [Bekannte Einschränkungen](#bekannte-einschränkungen)
- [Fehlerbehebung](#fehlerbehebung)
- [Hintergrund: Analyse von Boray POWer](#hintergrund-analyse-von-boray-power)
- [Lizenz](#lizenz)

---

## Funktionsweise

1. Das Programm prüft alle **2 Sekunden** alle TCP-Verbindungen auf dem LM-Studio-Port (Standard: **1234**).
2. Es zählt nur Verbindungen mit dem Status **`Established`**. Woher sie kommen (localhost, LAN, VPN, Mobil-App), spielt keine Rolle.
3. Solange mindestens eine Verbindung besteht, sendet das Programm per `SendInput` eine **Mausbewegung um 0 Pixel**, sofort und danach alle **10 Sekunden**. Der Mauszeiger bewegt sich dabei nicht.
4. Boray POWer erkennt Inaktivität über die Windows-Funktion `GetLastInputInfo`. Die simulierte Eingabe setzt diesen Zähler zurück, und Boray POWer schaltet auf bzw. bleibt auf **Höchstleistung**.
5. Endet die Anfrage, sendet das Programm keine Eingaben mehr. Boray POWer wartet dann wie gewohnt seine Leerlaufzeit ab und schaltet zurück auf **Energiesparen**.

```
LM-Studio-Client ──► Port 1234 (Established)
                          │
                LMStudioTrigger erkennt Verbindung
                          │
                SendInput (0-Pixel-Mausbewegung)
                          │
                GetLastInputInfo wird zurückgesetzt
                          │
          Boray POWer ──► Höchstleistung
```

---

## Voraussetzungen

| Komponente | Hinweis |
|---|---|
| Windows 10 | Getestet unter Windows 10 |
| Boray POWer V1.6.2 | Muss laufen und konfiguriert sein (Set high / Set low) |
| LM Studio | Lokaler Server aktiv, Standardport 1234 |
| .NET Framework 4.x | Bei Windows 10 bereits enthalten. Bringt den Compiler `csc.exe` mit. |

Es sind **keine Administratorrechte** nötig.

---

## Kompilieren

Der Quellcode ist in C# 5 geschrieben und lässt sich mit dem in Windows enthaltenen Compiler übersetzen. Es muss nichts installiert werden.

1. `LMStudioTrigger.cs` in einen Ordner legen.
2. In diesem Ordner PowerShell öffnen und ausführen:

```powershell
C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe /target:winexe /out:LMStudioTrigger.exe /r:System.Windows.Forms.dll /r:System.Drawing.dll LMStudioTrigger.cs
```

Der Hinweis des Compilers, dass er nur C# 5 unterstützt, ist normal und kein Fehler. Fehler erscheinen als Zeilen mit `error CS…`.

3. Prüfen, ob die EXE entstanden ist:

```powershell
Get-Item .\LMStudioTrigger.exe | Select-Object Name, Length, LastWriteTime
```

---

## Installation und Autostart

1. `LMStudioTrigger.exe` an einen festen Ort kopieren, z. B. neben `BorayPOWer.exe`.
2. `Win + R` → `shell:startup` → Enter.
3. Im geöffneten Autostart-Ordner eine **Verknüpfung** zur `LMStudioTrigger.exe` anlegen.

> **Wichtig:** Das Programm muss in **derselben angemeldeten Benutzersitzung** laufen wie Boray POWer. `GetLastInputInfo` arbeitet sitzungsbezogen. Als Windows-Dienst oder in einer anderen Sitzung wirkt die simulierte Eingabe nicht auf Boray POWer.

---

## Bedienung

Nach dem Start erscheint im Infobereich ein rundes **„AI“-Symbol**. Es kann auch unter dem Pfeil für ausgeblendete Symbole liegen.

| Symbol | Bedeutung |
|---|---|
| 🟢 Grün | LM Studio wird gerade benutzt, mindestens eine Verbindung aktiv |
| ⚪ Grau | Keine Anfrage |

Der Tooltip zeigt zusätzlich die Anzahl der aktiven Verbindungen.

**Rechtsklick-Menü:**

- **Status** öffnet ein Fenster mit:
  - überwachtem Port
  - aktuellem Zustand (aktiv / keine Anfrage)
  - Anzahl und Adressen der aktiven Verbindungen
  - Zeitpunkt der letzten Aktivität
  - Zeitpunkt der letzten simulierten Eingabe und deren Ergebnis (`1` = von Windows angenommen)
  - ggf. Fehlermeldung
- **Beenden** schließt das Programm.

Ein **Doppelklick** auf das Symbol öffnet ebenfalls den Status. Ein zweiter Start des Programms wird verhindert.

---

## Konfiguration

Die Einstellungen stehen als Konstanten am Anfang der Klasse `TriggerContext` in `LMStudioTrigger.cs`. Nach einer Änderung muss neu kompiliert werden.

```csharp
const int LmStudioPort = 1234;      // LM-Studio-Serverport
const int CheckIntervalMs = 2000;   // Prüfintervall der Verbindungen in ms
const int NudgeIntervalSec = 10;    // Abstand der simulierten Eingaben in s
```

> `NudgeIntervalSec` **muss kleiner** sein als die in Boray POWer eingestellte Leerlaufzeit. Sonst kann Boray POWer während einer laufenden Anfrage zurück in den Energiesparmodus schalten.

---

## Zeitlicher Ablauf

Beispiel mit Boray-POWer-Einstellungen **Leerlaufzeit 2 Minuten** und **CPU-Last-Schwelle 20 %**:

| Zeitpunkt | Ereignis |
|---|---|
| t = 0 s | Anfrage an LM Studio, Verbindung `Established` |
| ≤ 2 s | LM Studio Trigger erkennt die Verbindung und sendet eine Eingabe. Boray POWer schaltet auf Höchstleistung. |
| alle 10 s | Weitere Eingabe, solange die Verbindung besteht |
| Ende der Anfrage | Keine Eingaben mehr |
| + 2 min | Boray POWer prüft die durchschnittliche CPU-Last |
| CPU-Last ≤ 20 % | Wechsel auf Energiesparen |
| CPU-Last > 20 % | Boray POWer wartet erneut die Leerlaufzeit ab |

Laut Hersteller misst Boray POWer die CPU-Last als Mittelwert über 30 Sekunden und prüft sie nur vor dem Wechsel in den Energiesparmodus.

---

## Testen

### Test 1: Reagiert Boray POWer auf simulierte Eingaben?

Dieses Skript wartet, bis der Energiesparplan aktiv ist, sendet dann eine simulierte Eingabe und zeigt 10 Sekunden lang den aktiven Plan an. Für den Test die Leerlaufzeit in Boray POWer vorübergehend auf z. B. 30 Sekunden stellen und nach dem Start Maus und Tastatur nicht berühren.

Die GUID unten ist der Windows-Standardplan **Energiesparmodus**. Bei anderen Plänen anpassen (`powercfg /list`).

```powershell
$low = 'a1841308-3541-4fab-bc81-f71556f20b4a'
Add-Type @"
using System; using System.Runtime.InteropServices;
public static class In {
  [StructLayout(LayoutKind.Sequential)] public struct MI { public int dx, dy; public uint data, flags, time; public IntPtr extra; }
  [StructLayout(LayoutKind.Sequential)] public struct INP { public uint type; public MI mi; }
  [DllImport("user32.dll")] public static extern uint SendInput(uint n, INP[] i, int size);
  public static uint Nudge() { var a = new INP[1]; a[0].type = 0; a[0].mi.flags = 0x0001; return SendInput(1, a, Marshal.SizeOf(typeof(INP))); }
}
"@
"Maus und Tastatur NICHT beruehren. Warte auf Energiesparplan..."
while (-not ((powercfg /getactivescheme) -match $low)) { Start-Sleep 2 }
"$(Get-Date -f HH:mm:ss) Energiesparen aktiv. In 5 s folgt SendInput."
Start-Sleep 5
"SendInput Ergebnis: " + [In]::Nudge()
1..10 | ForEach-Object { Start-Sleep 1; "$(Get-Date -f HH:mm:ss) " + (powercfg /getactivescheme) }
```

Erwartetes Ergebnis: `SendInput Ergebnis: 1`, danach wird **Höchstleistung** angezeigt.

### Test 2: Praxistest mit LM Studio

1. `LMStudioTrigger.exe` starten.
2. Warten, bis Boray POWer im Energiesparmodus ist (Tray-Symbol von Boray POWer gedimmt).
3. Von einem anderen Gerät eine Anfrage an LM Studio senden, ohne den Server-PC zu berühren.
4. Das AI-Symbol wird grün, Boray POWer schaltet auf Höchstleistung.
5. Nach der Anfrage wird das AI-Symbol wieder grau. Nach Ablauf der Boray-Leerlaufzeit wechselt der Plan zurück.

Nach dem Test die Leerlaufzeit in Boray POWer wieder auf den gewohnten Wert stellen.

---

## Bekannte Einschränkungen

- **Verbindung statt Inhalt:** Das Programm erkennt nur, dass eine Verbindung besteht, nicht was angefragt wird. Jede Verbindung zu Port 1234 zählt, auch z. B. reine Modelllisten-Abfragen.
- **Dauerhaft offene Verbindungen:** Hält ein Client die Verbindung nach der Anfrage offen (Keep-Alive), bleibt das Symbol grün und der PC auf Höchstleistung. In diesem Fall im Status-Fenster prüfen, welche Adresse die Verbindung hält.
- **Bildschirmschoner und Monitor:** Die simulierte Eingabe setzt auch die Zeitgeber für Bildschirmschoner und Monitorabschaltung zurück. Während einer Anfrage schaltet sich der Monitor daher nicht ab.
- **Sitzungsgebunden:** Funktioniert nur in der angemeldeten Benutzersitzung, nicht als Dienst.
- **Sperrbildschirm / abgemeldeter Benutzer:** Nicht getestet.

---

## Fehlerbehebung

| Problem | Mögliche Ursache / Lösung |
|---|---|
| Kein Tray-Symbol | Unter dem Pfeil für ausgeblendete Symbole nachsehen |
| „läuft bereits“ beim Start | Programm ist schon aktiv, siehe Infobereich |
| Symbol wird bei Anfrage nicht grün | Port prüfen (`LmStudioPort`), LM-Studio-Server aktiv? |
| Symbol grün, Boray POWer schaltet nicht | Läuft das Programm in derselben Benutzersitzung wie Boray POWer? Im Status das Ergebnis der simulierten Eingabe prüfen (`1` = ok, `0` = von Windows abgelehnt) |
| Symbol bleibt nach der Anfrage grün | Client hält die Verbindung offen, siehe [Bekannte Einschränkungen](#bekannte-einschränkungen) |
| Boray POWer schaltet während der Anfrage zurück | `NudgeIntervalSec` ist nicht kleiner als die Boray-Leerlaufzeit |

Verbindungen auf Port 1234 lassen sich auch manuell anzeigen:

```powershell
Get-NetTCPConnection -LocalPort 1234 -ErrorAction SilentlyContinue |
  Where-Object State -ne 'Listen' |
  Select-Object RemoteAddress, RemotePort, State, OwningProcess
```

---

## Hintergrund: Analyse von Boray POWer

Die Lösung basiert auf einer Untersuchung der Metadaten und eingebetteten Zeichenketten von `BorayPOWer.exe` V1.6.2. Das Programm wurde dafür nicht decompiliert oder verändert.

| Punkt | Befund |
|---|---|
| Sprache | VB.NET (Visual Studio 2008) |
| Framework | .NET Framework 2.0 |
| Inaktivitätserkennung | `GetLastInputInfo` aus `user32.dll` |
| Planwechsel | `powercfg -setactive <GUID>`, Abfrage über `powercfg -getactivescheme` |
| CPU-Last | Leistungsindikator `% Processor Time`, Mittelwert über 30 Sekunden |
| `Interop.IWshRuntimeLibrary.dll` | Nur für die Autostart-Verknüpfung von Boray POWer |

Weil Boray POWer `GetLastInputInfo` nutzt, wirkt eine per `SendInput` erzeugte Eingabe wie echte Benutzeraktivität. Dadurch ist keine Änderung an Boray POWer nötig.

---

## Lizenz

**LM Studio Trigger:** _Lizenz noch festzulegen._

**Boray POWer** ist **nicht** Bestandteil dieses Repositorys und wird hier nicht verteilt. Boray POWer © Anders Persson. Laut Programmhinweis ist es „Boray Music Ware“: Wer es nutzt, soll mindestens einen Titel des Autors kaufen. Download und Informationen: <http://www.boray.se/software/boraypower/>

LM Studio ist ein Produkt der jeweiligen Rechteinhaber. Dieses Projekt steht in keiner Verbindung zu Boray Software oder LM Studio.
