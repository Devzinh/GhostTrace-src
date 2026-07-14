# GhostTrace

![GhostTrace](../assets/readme/hero.svg)

> **Encontre os rastros que um software deixou no Windows, antes que eles voltem a incomodar.**

GhostTrace e um caçador local de rastros forenses para Windows, feito para analistas, administradores de sistemas e pessoas que levam a seguranca do proprio ambiente a serio. Ele mapeia persistencia, execucao, atividade e residuos de software em um registro revisavel, oferecendo limpeza estritamente controlada apenas quando voce escolhe faze-la.

[![CI](https://github.com/Devzinh/GhostTrace-src/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/Devzinh/GhostTrace-src/actions/workflows/ci.yml)
[![Release](https://img.shields.io/github/v/release/Devzinh/GhostTrace-src?display_name=tag&sort=semver&style=flat-square)](https://github.com/Devzinh/GhostTrace-src/releases/latest)
![Windows](https://img.shields.io/badge/plataforma-Windows%2010%2F11%20x64-1F8FFF?style=flat-square)
![.NET](https://img.shields.io/badge/.NET-10-17B47E?style=flat-square)
[![Licenca](https://img.shields.io/badge/licenca-MIT-F39C3D?style=flat-square)](../LICENSE)

**[Download](https://github.com/Devzinh/GhostTrace-src/releases/latest)** | **[Inicio rapido](#inicio-rapido)** | **[Capacidades](#o-que-ele-coleta)** | **[Seguranca](#seguranca-forense)** | **[Roadmap](roadmap.md)** | **[English](../README.md)**

---

GhostTrace responde uma pergunta simples: **o que ainda ficou no sistema depois que um software foi removido?**

Em uma unica execucao, a ferramenta coleta evidencia de persistencia, execucao, atividade de usuario e artefatos do sistema. Os achados ficam separados por modulo, podem ser exportados como registros locais e so viram candidatos a limpeza quando passam por regras de confianca restritas.

> GhostTrace nao trata um achado como prova conclusiva de comprometimento. Ele preserva e organiza evidencia para a sua analise.

![GhostTrace em execucao](../assets/readme/demo.gif)

## Inicio rapido

### Instale

Baixe o MSI x64 mais recente em [Releases](https://github.com/Devzinh/GhostTrace-src/releases/latest):

```text
GhostTrace-<version>-x64.msi
```

O pacote e **self-contained**, portanto a maquina alvo nao precisa ter o runtime .NET instalado. Execute o GhostTrace como Administrador para acessar artefatos protegidos do Windows.

### Investigue um nome

```powershell
GhostTrace.CLI scan --name nvidia
```

O modo interativo mostra o progresso, consolida achados e permite exportar o registro da investigacao. Caso existam residuos elegiveis, a limpeza continua opcional: voce escolhe os itens e digita uma frase de confirmacao antes de qualquer remocao.

### Automatize uma coleta

```powershell
GhostTrace.CLI scan --name nvidia --quiet --output C:\Cases\Host1
```

`--quiet` nao abre prompts e grava um relatorio TXT. Se o relatorio nao puder ser persistido, o comando retorna um codigo de erro em vez de sinalizar sucesso silenciosamente.

### Comandos essenciais

| Objetivo | Comando |
| --- | --- |
| Abrir o menu interativo | `GhostTrace.CLI` |
| Executar triagem completa | `GhostTrace.CLI scan` |
| Procurar rastros de um software | `GhostTrace.CLI scan --name <nome>` |
| Criar um registro para automacao | `GhostTrace.CLI scan --name <nome> --quiet --output <diretorio>` |
| Correlacionar COM e TaskCache | `GhostTrace.CLI scan-tasks-correlate-json --output <relatorio.json>` |
| Inspecionar diretorio, Registro ou Event Log | `scan-fs-json`, `scan-reg-json`, `scan-evt-json` |

Escolha o idioma da interface com `--lang`:

```powershell
GhostTrace.CLI scan --name nvidia --lang pt-BR
GhostTrace.CLI --lang en
```

## Da coleta a revisao

![Fluxo de investigacao](../assets/readme/workflow.svg)

1. GhostTrace executa os modulos disponiveis para a coleta escolhida.
2. Cada modulo retorna seus proprios achados, erros e metadados.
3. A CLI renderiza um resumo e grava um registro local quando solicitado.
4. Qualquer limpeza selecionada ganha um log de auditoria separado.

## O que ele coleta

![Cobertura de modulos](../assets/readme/modules.svg)

| Area | Fontes de evidencia |
| --- | --- |
| Persistencia | Run/RunOnce, Startup, servicos, Winlogon, IFEO, AppInit, LSA, Active Setup, WMI e tarefas agendadas |
| Execucao | Prefetch, Shimcache, BAM/DAM, UserAssist e MUICache |
| Atividade | Historico PowerShell, RDP de saida, RecentDocs, USB e rede |
| Software instalado e residuos | Entradas de uninstall, StartupApproved, Program Files, ProgramData e AppData |
| Correlacao de tarefas | Divergencias entre Task Scheduler COM e TaskCache para investigar Ghost Tasks (T1053.005) |

### Modulos de persistencia

| Modulo | Fonte |
| --- | --- |
| `PersistenceScanModule` | Run/RunOnce e pastas Startup |
| `ServicesScanModule` | Valores `ImagePath` de servicos e drivers |
| `AsepScanModule` | Winlogon, IFEO, AppInit, LSA e Active Setup |
| `ScheduledTasksScanModule` | Task Scheduler COM, incluindo tarefas ocultas |
| `TaskCacheScanModule` | Anomalias em `TaskCache\Tree` |
| `WmiPersistenceScanModule` | `__EventFilter`, `__EventConsumer` e bindings |

### Modulos de execucao e atividade

| Modulo | Fonte |
| --- | --- |
| `PrefetchScanModule` | Arquivos `.pf` do Windows 10/11, incluindo XPRESS-Huffman |
| `ShimcacheScanModule` | AppCompatCache |
| `BamScanModule` | Registros BAM/DAM por SID |
| `UserAssistScanModule` | Lancamentos GUI e contagens de uso |
| `MuiCacheScanModule` | MUICache do shell |
| `PowerShellHistoryScanModule` | Historico PSReadLine e sinais de comandos suspeitos |
| `RdpConnectionScanModule` | Historico de conexoes RDP de saida |
| `RecentDocsScanModule` | RecentDocs do Explorer |
| `UsbDeviceScanModule` | Historico USBSTOR |
| `NetworkArtifactsScanModule` | Arquivo hosts e perfis de rede conhecidos |

## Seguranca forense

GhostTrace e um coletor **somente leitura por padrao**. A limpeza foi desenhada para residuos de software, nao para remediacao automatica de malware.

- A coleta nao transmite dados, exige nuvem ou depende de uma conta.
- Nenhum item de limpeza vem preselecionado: a escolha e manual e requer confirmacao digitada.
- Caches de execucao e historicos ficam fora da limpeza para preservar evidencia.
- Um diretorio so pode ser removido se for filho direto de uma root confiavel, corresponder exatamente ao alvo e nao for junction ou symlink.
- Correspondencias parciais se tornam `FilesystemTraceHint`: entram no relatorio, nunca na limpeza.
- Relatorios JSON usam gravacao atomica; um relatorio existente so e substituido depois que o novo termina de ser escrito.
- `Ctrl+C` cancela cooperativamente a correlacao de tarefas e a leitura de Prefetch.

> Um achado e evidencia, nao veredito. Interprete-o com a linha do tempo do host, o contexto do ambiente e seu processo de resposta a incidentes.

## Saidas para a investigacao

| Saida | Melhor uso |
| --- | --- |
| Tabela interativa | Revisao rapida por analistas |
| Relatorio TXT | Registro local do `scan`, inclusive com automacao `--quiet` |
| Relatorio JSON | Coletores direcionados e correlacao de tarefas |
| Log de limpeza | Auditoria de itens removidos, ignorados ou com erro |

`PartialSuccess` significa que um modulo produziu achados, mas tambem encontrou uma limitacao. Leia os erros desse modulo antes de considerar uma fonte sem resultado como limpa.

## Feito para inspirar confianca

- Pull requests executam restore, build e testes em Windows com .NET 10.
- O gate de release testa toda a `GhostTrace.sln` antes de construir o MSI.
- `src/GhostTrace.Tests` e `tests/GhostTrace.Tests.Unit` fazem parte da solucao e da CI.
- Releases estaveis aceitam somente tags `v<major>.<minor>.<patch>`.
- GhostTrace e distribuido sob a [Licenca MIT](../LICENSE).

## Historico de estrelas

[![Star History Chart](https://api.star-history.com/svg?repos=Devzinh/GhostTrace-src&type=Date)](https://www.star-history.com/#Devzinh/GhostTrace-src&Date)

## Documentacao

- [Playbook de correlacao de tarefas agendadas](playbooks/scheduled-tasks-correlation.md)
- [Roadmap do produto](roadmap.md)
- [Decisoes de UX e arquitetura](design/ux-architecture-decisions.md)
- [Guia dos projetos de teste](../tests/README.md)
- [README em ingles](../README.md)

## Contribuindo

Contribuicoes sao bem-vindas. Mantenha coletores read-only, propague `CancellationToken`, exponha lacunas de cobertura como erros de resultado e inclua testes com mudancas de comportamento. O [roadmap](roadmap.md) mostra as proximas areas de maior impacto.

---

GhostTrace apoia a investigacao. Valide cada artefato no contexto do host, de sua linha do tempo e dos procedimentos do seu ambiente.
