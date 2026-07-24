# AsusFanControlKimera

Interfaccia Windows Forms completa costruita sopra il motore hardware funzionante
di AsusFanControl.

## Funzioni

- modalità sistema ASUS, manuale e curva;
- curva grafica modificabile e formato testuale `temperatura,percentuale`;
- isteresi e intervallo di aggiornamento configurabili;
- lettura temperatura CPU e RPM di tutte le ventole;
- limiti di sicurezza opzionali (40–99%);
- area di notifica, avvio minimizzato e avvio con Windows;
- rilascio opzionale del controllo ventole all'uscita.
- debug opzionale con registro diagnostico circolare.

## Compilazione

Aprire `AsusFanControlKimera.sln` con Visual Studio e compilare `Release|x64`.
È richiesto il targeting pack di .NET Framework 4.7.2.

La DLL `AsusWinIO64.dll` deve trovarsi accanto all'eseguibile. Su questo sistema
la DLL ASUS restituisce dati validi soltanto sotto l'account `SYSTEM`. Kimera si
eleva prima come amministratore e poi si rilancia tramite il `PsExec.exe` già
installato in `C:\Program Files (x86)\AsusFanControl`, replicando il comportamento
del `run.bat` dell'applicazione funzionante. PsExec non è incluso nel progetto.

Kimera conserva il SID dell'utente interattivo prima del rilancio come SYSTEM.
L'opzione “Avvia con Windows” scrive quindi nel profilo dell'utente reale; al
login viene comunque richiesta la conferma UAC necessaria per PsExec. Un mutex
globale impedisce a sessioni Windows diverse di controllare contemporaneamente
lo stesso hardware.

## Sicurezza

Con “Limiti sicuri” attivo, le modalità manuale e curva applicano valori compresi
tra 40% e 99%. La modalità “Sistema ASUS” usa il valore speciale 0 per disattivare
il test mode e restituire il controllo al firmware.

Kimera passa automaticamente alla modalità sistema ASUS quando rileva:

- due temperature consecutive fuori dall'intervallo plausibile 10–115 °C;
- tre errori consecutivi durante la lettura dell'hardware;
- una ventola a 0 RPM per tre letture consecutive mentre il PWM comandato è
  almeno 40%.

In caso di errore durante un comando multi-ventola, il controller tenta inoltre
di disattivare il test mode su tutte le ventole precedentemente rilevate. La
disattivazione dei limiti sicuri richiede una conferma esplicita.

Anche la disattivazione del rilascio all'uscita richiede conferma, perché
l'ultimo PWM potrebbe rimanere attivo dopo la chiusura.

Queste protezioni coprono gli errori gestibili. Un arresto forzato del processo,
un blocco nella DLL nativa o una perdita improvvisa di alimentazione non possono
essere gestiti con certezza da un'applicazione nello stesso processo.

## Registro diagnostico

`Opzioni > Debug` abilita un registro in:

`C:\ProgramData\AsusFanControlKimera\kimera-debug.log`

Il file contiene snapshot di temperatura, RPM e PWM, cambi di modalità, errori
e attivazioni fail-safe. La dimensione massima è 1 MB; raggiunto il limite,
Kimera conserva automaticamente le righe più recenti per circa 768 KB. Qualunque
errore di scrittura del registro viene ignorato per non interferire con il
controllo delle ventole.

Quando si attiva un fail-safe appare una finestra persistente con ora, causa,
ventole coinvolte, ultimi valori osservati ed esito del rilascio al firmware.
