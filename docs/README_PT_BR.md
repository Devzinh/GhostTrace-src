# GhostTrace

> **Encontre o que o software deixou para trás no Windows, antes que ele encontre o caminho de volta.**

O GhostTrace é um caçador de vestígios forenses locais no Windows para analistas de resposta a incidentes, administradores de sistema e usuários avançados focados em segurança. Ele mapeia persistência, execução, atividade e artefatos remanescentes em um registro auditável, oferecendo depois uma limpeza estritamente controlada apenas quando você escolher explicitamente.

**[Download](https://github.com/Devzinh/GhostTrace/releases/latest)** | **[Requisitos](https://www.google.com/search?q=%23requisitos)** | **[Início rápido](https://www.google.com/search?q=%23in%C3%ADcio-r%C3%A1pido)** | **[Capacidades](https://www.google.com/search?q=%23o-que-ele-coleta)** | **[Segurança forense](https://www.google.com/search?q=%23seguran%C3%A7a-forense)** | **[Roadmap](https://www.google.com/search?q=docs/roadmap.md)** | **[Português (Brasil)](https://www.google.com/search?q=docs/README_PT_BR.md)**

---

## Por que o GhostTrace

Desinstalar um aplicativo nem sempre remove sua pegada operacional. Entradas de inicialização, tarefas agendadas, evidências de execução em cache, valores do Registro, serviços e pastas podem permanecer muito tempo depois que o instalador informa conclusão com sucesso.

O GhostTrace transforma essa questão abrangente em uma investigação local e focada:

* **Uma varredura focada** em  NN módulos forenses.
* **Evidências por fonte**, com achados, erros e metadados mantidos por módulo.
* **Offline por design**, sem telemetria, uploads ou dependência de nuvem.
* **Saída pronta para auditoria**, incluindo registros em TXT, saídas em JSON para coletores direcionados e logs de limpeza.
* **Limpeza controlada por humanos**, nunca remediação automática.

## Requisitos

| Item | Requisito |
| --- | --- |
| Sistema operacional | Windows 10 ou 11, x64 |
| Privilégios | Administrador, necessário para ler artefatos protegidos |
| Runtime | Nenhum. O MSI é autônomo (*self-contained*). |
| Disco |  ~NN MB instalado, mais espaço para a saída do relatório |

## Verifique seu download

O GhostTrace lê artefatos protegidos do Windows e executa com privilégios elevados. Verifique o instalador antes de executá-lo, especialmente em um host que você esteja investigando.

```powershell
Get-FileHash .\GhostTrace-1.5.0-x64.msi -Algorithm SHA256

```

Compare o resultado com o arquivo `SHA256SUMS.txt` publicado em cada release.

O instalador ainda não possui assinatura de código (*code-signing*), portanto o SmartScreen e alguns produtos de EDR podem sinalizá-lo na primeira execução. Verifique o hash em vez de apenas ignorar o aviso.

## Início rápido

### Instalação

Baixe o MSI x64 mais recente em [Releases](https://github.com/Devzinh/GhostTrace/releases/latest):

```text
GhostTrace-<version>-x64.msi

```

O pacote é autônomo, portanto a máquina de destino não precisa de um runtime .NET pré-instalado. Execute o GhostTrace como Administrador para acessar artefatos protegidos do Windows.

### Investigar o nome de um software

```powershell
GhostTrace.CLI scan --name nvidia

```

O fluxo interativo mostra o progresso, agrupa os achados por técnica e pode exportar um registro da investigação. Se existirem candidatos seguros para limpeza, você deve selecioná-los e digitar uma frase de confirmação antes que qualquer remoção ocorra.

Um exemplo resumido do resultado produzido:

```text
$ GhostTrace.CLI scan --name nvidia

  MODULE                        FINDINGS  STATUS
  PersistenceScanModule                2  OK
  ScheduledTasksScanModule             1  OK
  TaskCacheScanModule                  1  PartialSuccess
  PrefetchScanModule                   4  OK

  [Persistence] HKLM\...\Run\NvBackend
      -> C:\Program Files\NVIDIA Corporation\Update Core\NvBackend.exe (missing)

  [Ghost Task] TaskCache\Tree entry with no COM counterpart (T1053.005)
      \NvProfileUpdaterOnLogon_{...}

  TaskCacheScanModule: PartialSuccess. 2 keys unreadable (access denied).

```

### Executar em automação

```powershell
GhostTrace.CLI scan --name nvidia --quiet --output C:\Cases\Host1

```

O parâmetro `--quiet` cria um registro TXT não interativo. Falhas na gravação do relatório retornam um código de saída diferente de zero, garantindo que a automação não passe silenciosamente sem gerar um artefato.

### Comandos essenciais

| Objetivo | Comando |
| --- | --- |
| Abrir o menu interativo | `GhostTrace.CLI` |
| Executar uma triagem completa | `GhostTrace.CLI scan` |
| Buscar por um aplicativo específico | `GhostTrace.CLI scan --name <n>` |
| Gravar um registro de varredura amigável para scripts | `GhostTrace.CLI scan --name <n> --quiet --output <diretório>` |
| Correlacionar COM e TaskCache do Agendador de Tarefas | `GhostTrace.CLI scan-tasks-correlate-json --output <relatório.json>` |
| Inspecionar um diretório, chave do Registro ou Log de Eventos | `scan-fs-json`, `scan-reg-json`, `scan-evt-json` |

Escolha o idioma da interface com `--lang`:

```powershell
GhostTrace.CLI scan --name nvidia --lang en
GhostTrace.CLI --lang pt-BR

```

### Códigos de saída

| Código | Significado |
| --- | --- |
| `0` | Varredura concluída e qualquer relatório solicitado foi gravado |
| `1` | Argumentos inválidos ou ambiente não suportado |
| `2` | Falha na gravação do relatório |
| `3` | Cancelado pelo operador |

## Da coleta à revisão

1. O GhostTrace executa os módulos disponíveis para a coleta selecionada.
2. Cada módulo retorna seus próprios achados, erros e metadados.
3. A CLI exibe um resumo conciso e grava um registro local quando solicitado.
4. Qualquer operação de limpeza selecionada é gravada em um log de auditoria separado.

## O que ele coleta

| Área | Fontes de evidência |
| --- | --- |
| Persistência | Run/RunOnce, Startup, serviços, Winlogon, IFEO, AppInit, LSA, Active Setup, WMI e tarefas agendadas |
| Execução | Prefetch, Shimcache, BAM/DAM, UserAssist e MUICache |
| Atividade do usuário | Histórico do PowerShell, histórico de RDP de saída, RecentDocs, USB e artefatos de rede |
| Software instalado e vestígios | Entradas de desinstalação, StartupApproved, vestígios em Program Files, ProgramData e AppData |
| Correlação de tarefas agendadas | Discrepâncias entre COM e TaskCache usadas para investigar Ghost Tasks (T1053.005) |

**Persistência**

| Módulo | Fonte |
| --- | --- |
| `PersistenceScanModule` | Pastas Run/RunOnce e Startup |
| `ServicesScanModule` | Valores de `ImagePath` de serviços e drivers |
| `AsepScanModule` | Winlogon, IFEO, AppInit, LSA e Active Setup |
| `ScheduledTasksScanModule` | COM do Agendador de Tarefas, incluindo tarefas ocultas |
| `TaskCacheScanModule` | Anomalias em `TaskCache\Tree` |
| `WmiPersistenceScanModule` | `__EventFilter`, `__EventConsumer` e vinculações (*bindings*) |

**Execução e atividade**

| Módulo | Fonte |
| --- | --- |
| `PrefetchScanModule` | Arquivos `.pf` do Windows 10/11, incluindo compressão XPRESS-Huffman |
| `ShimcacheScanModule` | AppCompatCache |
| `BamScanModule` | Registros BAM/DAM por SID |
| `UserAssistScanModule` | Execuções via GUI e contagem de uso |
| `MuiCacheScanModule` | MUICache do Shell |
| `PowerShellHistoryScanModule` | Histórico do PSReadLine e sinais de comandos suspeitos |
| `RdpConnectionScanModule` | Histórico de conexões RDP de saída |
| `RecentDocsScanModule` | RecentDocs do Explorer |
| `UsbDeviceScanModule` | Histórico do USBSTOR |
| `NetworkArtifactsScanModule` | Arquivo hosts e perfis de rede conhecidos |

## Segurança forense

O GhostTrace é um **coletor somente leitura por padrão**. Seu fluxo de trabalho de limpeza é focado em vestígios de software, não em remediação automática de malware.

* Nenhuma chamada de rede, telemetria, envio de evidências ou conta em nuvem é necessária.
* A limpeza começa sem nenhum item pré-selecionado e exige seleção explícita mais confirmação digitada.
* Caches de execução e históricos de atividade nunca são candidatos à limpeza.
* Um diretório só é removível quando estiver diretamente sob uma raiz confiável, corresponder exatamente ao nome do alvo e não for um *junction* ou *symlink*.
* Correspondências parciais de nome tornam-se `FilesystemTraceHint`: reportáveis, mas nunca removíveis.
* Relatórios JSON são gravados de forma atômica, preservando o relatório existente até que a substituição seja concluída.
* `Ctrl+C` cancela de forma cooperativa a correlação de tarefas agendadas e a leitura de arquivos Prefetch.

> Um achado é uma evidência, não um veredito. Interprete-o juntamente com a linha do tempo do host, seu ambiente e seu processo de resposta a incidentes.

### O que o GhostTrace NÃO é

* Não é um antivírus nem uma ferramenta de remediação de malware.
* Não é uma ferramenta de computação forense de memória. Ele lê apenas artefatos de disco e do Registro.
* Não é um coletor remoto. Ele inspeciona o host local.
* Não é um motor de vereditos. Ele reporta evidências para que um analista as interprete.

## Saídas adequadas para a investigação

| Saída | Recomendado para |
| --- | --- |
| Tabela interativa | Revisão rápida pelo analista |
| Relatório TXT | Registro local do `scan`, incluindo automação com `--quiet` |
| Relatório JSON | Coletores direcionados e correlação de tarefas agendadas |
| Log de limpeza | Auditoria de ações de limpeza removidas, ignoradas ou com falha |

`PartialSuccess` significa que um módulo gerou achados, mas também encontrou alguma limitação. Leia os erros desse módulo antes de interpretar um resultado ausente como uma fonte limpa.

## Desenvolvido para ser confiável

* Pull requests restauram, compilam e testam no Windows com .NET 10.
* A etapa de lançamento testa a solução `GhostTrace.sln` completa antes de compilar o MSI.
* Tanto `src/GhostTrace.Tests` quanto `tests/GhostTrace.Tests.Unit` estão incluídos na solução e no CI.
* Versões estáveis aceitam apenas tags no formato `v<major>.<minor>.<patch>`.
* O GhostTrace é distribuído sob a [Licença MIT](https://www.google.com/search?q=LICENSE).

## Desinstalação

Remova o GhostTrace em **Configurações > Aplicativos > Aplicativos instalados**, ou a partir de um prompt elevado:

```powershell
msiexec /x GhostTrace-1.5.0-x64.msi /qn

```

Relatórios e logs de limpeza gravados no seu diretório de saída não são removidos pelo desinstalador. Exclua-os manualmente após o encerramento do caso.

## Documentação

* [Playbook de Correlação de Tarefas Agendadas](https://www.google.com/search?q=docs/playbooks/scheduled-tasks-correlation.md)
* [Roadmap do produto](https://www.google.com/search?q=docs/roadmap.md)
* [Decisões de UX e arquitetura](https://www.google.com/search?q=docs/design/ux-architecture-decisions.md)
* [Guia do projeto de testes](https://www.google.com/search?q=tests/README.md)
* [README em Português (Brasil)](https://www.google.com/search?q=docs/README_PT_BR.md)

## Contribuição

Contribuições são bem-vindas. Mantenha os coletores como somente leitura, propague o `CancellationToken`, informe lacunas de cobertura como erros no resultado e inclua testes para mudanças de comportamento. O [roadmap](https://www.google.com/search?q=docs/roadmap.md) destaca as próximas áreas de alto impacto.

---

O GhostTrace auxilia na investigação. Valide cada artefato no contexto do host, de sua linha do tempo e de seus procedimentos operacionais.
