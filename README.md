# GhostTrace

![GhostTrace Hero](assets/readme/hero.svg)

<p align="center">
  <a href="#-por-que-ghosttrace">Por que GhostTrace</a> •
  <a href="#-fluxo-da-investigacao">Fluxo</a> •
  <a href="#-instalacao-e-uso-rapido">Uso rapido</a> •
  <a href="#-cobertura-forense">Cobertura</a> •
  <a href="#-seguranca-forense">Seguranca</a>
</p>

<p align="center">
  <img alt="Platform" src="https://img.shields.io/badge/Platform-Windows%2010%2F11%20x64-1F8FFF?style=for-the-badge" />
  <img alt="Runtime" src="https://img.shields.io/badge/.NET-10-17B47E?style=for-the-badge" />
  <img alt="Interface" src="https://img.shields.io/badge/UI-Spectre.Console-0E1A2F?style=for-the-badge" />
  <img alt="Mode" src="https://img.shields.io/badge/Scan-Read--Only-F39C3D?style=for-the-badge" />
</p>

---

> **"Desinstalou? Ele ainda ta aqui, bestinha."**

Voce clicou em *Desinstalar*. O Windows disse que foi. O software disse tchau.
Mas aquele processo ainda ta no registro, na pasta, no prefetch, na tarefa agendada, no historico do RDP, no WMI, no—

Pois e. Nao foi.

**GhostTrace e o cara que nao aceita o "desinstalado" sem provas.**
22 modulos forenses, uma passada so, tabela limpa, sem papo.

---

## Por que GhostTrace

Skill issue dos outros scanners: ou sao paidos demais, ou te jogam um log de 4000 linhas e te deixam sozinho.

GhostTrace nao.

- **Rapido** — dezenas de tecnicas forenses numa varredura unica. Sem ficar esperando barra de progresso ser criativa.
- **Claro** — resultado em tabela unica. Nao e Sherlock Holmes, e profissional.
- **Seguro** — read-only por default. Nao apaga nada sem voce falar SIM com todas as letras.
- **Auditavel** — cada limpeza vira log. Voce tem recibo.
- **Offline** — zero telemetria. Zero upload. Zero "mandando pra nuvem". Seu PC, sua vida.

---

## Fluxo da investigacao

![Fluxo de investigacao](assets/readme/workflow.svg)

```text
voce: "quero investigar o nvidia"
GhostTrace: OK watch this
  -> aciona 22 modulos forenses
  -> monta tabela com tudo que encontrou
  -> pergunta se voce quer limpar
  -> se sim: confirma, remove, gera log
  -> se nao: te devolve os dados e tchau
  -> opcional: exporta relatorio TXT
```

Sem magia. Sem achismo. So forense com UX decente.

---

## Instalacao e uso rapido

```text
GhostTrace.CLI                                              # menu interativo (precisa de admin, nao questiona)
GhostTrace.CLI scan --name nvidia                          # caca ao nvidia, limpeza opcional
GhostTrace.CLI scan --name nvidia --quiet --output C:\Cases\Host1
GhostTrace.CLI scan                                        # triagem completa, sem filtro
GhostTrace.CLI scan-fs-json  <dir> <out.json>
GhostTrace.CLI scan-reg-json <hive> <subkey> <out.json>
GhostTrace.CLI scan-evt-json <log> <maxEntries> <out.json>
```

Precisa de UAC porque le areas do sistema que o Windows esconde do usuario comum.
Nao e pedancia. E necessario.

Forcando idioma se quiser ser diferentao:

```text
GhostTrace.CLI --lang es
GhostTrace.CLI scan --name nvidia --lang en
```

---

## Cobertura forense

![Cobertura de modulos](assets/readme/modules.svg)

22 modulos. Cada um e um especialista diferente da mesma investigacao.
Nenhum fica de fora da cena do crime.

### Persistencia (MITRE ATT&CK TA0003)

> Coisas que tentam voltar quando o Windows liga. Classico vilao.

| Modulo | O que investiga |
| --- | --- |
| `PersistenceScanModule` | Chaves Run/RunOnce + pastas Startup |
| `ServicesScanModule` | Servicos e drivers com ImagePath suspeito (T1543.003) |
| `AsepScanModule` | Winlogon, IFEO debugger, AppInit_DLLs, LSA packages, Active Setup |
| `ScheduledTasksScanModule` | Tarefas agendadas via COM API do Task Scheduler |
| `TaskCacheScanModule` | Anomalias em TaskCache\Tree, incluindo Ghost Tasks (T1053.005) |
| `WmiPersistenceScanModule` | __EventFilter, __EventConsumer e binding (T1546.003) |

### Evidencia de execucao (TA0002)

> O programa disse que nunca esteve aqui. Mentira. O prefetch entregou.

| Modulo | O que investiga |
| --- | --- |
| `ShimcacheScanModule` | AppCompatCache (formato 10ts para Win8.1/10/11) |
| `PrefetchScanModule` | Arquivos .pf com decode XPRESS-Huffman (versoes 26/30/31) |
| `BamScanModule` | BAM/DAM com ultimo tempo de execucao por SID |
| `UserAssistScanModule` | Lancamentos GUI com contagem e ultimo run (ROT13) |
| `MuiCacheScanModule` | MUICache do shell e nomes amigaveis embutidos |

### Atividade do usuario e artefatos de sistema

> O que rolou na rotina do PC. Tudo. Incluindo o que voce esqueceu.

| Modulo | O que investiga |
| --- | --- |
| `PowerShellHistoryScanModule` | Historico PSReadLine e sinais de download cradle/payload encoded (T1059.001) |
| `RdpConnectionScanModule` | Historico de conexoes RDP de saida e pistas de usuario (T1021.001) |
| `RecentDocsScanModule` | Arquivos/pastas recentes por usuario (Explorer RecentDocs) |
| `UsbDeviceScanModule` | Historico de dispositivos removiveis via USBSTOR (T1052/T1091) |
| `NetworkArtifactsScanModule` | Redirecionamentos no hosts e redes conectadas com datas |

### Software instalado e residuos em disco

> O famoso "desinstalei" mas a pasta ainda ta la. Capturado.

| Modulo | O que investiga |
| --- | --- |
| `UninstallEntriesScanModule` | Programas instalados, versao, publisher, local e uninstall string |
| `StartupApprovedScanModule` | Estado habilitado/desabilitado de entradas de inicializacao |
| `FileSystemTraceScanModule` | Busca orientada por nome em Program Files, ProgramData e AppData |

### Coletores direcionados (per-target)

> Voce aponta o dedo. GhostTrace investiga.

| Modulo | O que investiga |
| --- | --- |
| `FilesystemScanModule` | Metadados de arquivos sob um diretorio |
| `RegistryScanModule` | Valores sob uma chave de registro |
| `EventLogScanModule` | Entradas recentes de logs Application/System |

---

## Seguranca forense

Sem jumpscare. Sem "ops deletei sem querer".

- **Scan read-only** — so olha. Nao toca em nada.
- **Limpeza com consentimento** — so remove depois de voce escrever SIM. Sem confirmacao implicita, sem clique acidental.
- **Evidencia protegida** — caches de execucao e historicos ficam fora da limpeza. Nao se destroi prova.
- **Offline total** — sem chamadas de rede. Sem telemetria. Sem surpresa no Wireshark.
- **Sinal suspeito nao e sentenca** — e dado para analise, nao veredito automatico. Voce investiga, voce decide.

---

## Idiomas (I18N)

Funciona igual streaming e jogo: detecta o idioma do sistema e ja abre no seu.
Se o sistema e portugues, ele e portugues. Se nao tiver suporte, cai pro ingles. Simples.

Disponiveis:

- English (en-US)
- Portugues Brasil (pt-BR)
- Espanol (es-ES)

Instalador localizado: `GhostTrace-<version>-<culture>-x64.msi`

---

## Playbooks e documentacao

- [Scheduled Tasks Correlation Playbook](docs/playbooks/scheduled-tasks-correlation.md) — investigacao de Ghost Tasks e manipulacao em registro.

---

**Se o seu objetivo e "quero saber o que sobrou de verdade, nao no achismo":**

GhostTrace e esse cara. Projeto serio. Visual moderno. Zero enrolacao.
