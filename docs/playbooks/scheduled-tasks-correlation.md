# GhostTrace: Scheduled Tasks Correlation Playbook

## Objetivo
Este documento detalha o funcionamento, execução e interpretação do motor de correlação de Tarefas Agendadas (Scheduled Tasks) no GhostTrace (v1). O objetivo principal desta feature é a detecção heurística e forensic-safe de **Ghost Tasks** (MITRE ATT&CK T1053.005) geradas por manipulação direta do Registro do Windows.

## O Problema Forense: Evasão em Scheduled Tasks
Atacantes frequentemente abusam do Agendador de Tarefas para persistência. Uma técnica avançada (conhecida como *Task Hiding* ou *Ghost Tasks*) envolve a deleção intencional do Security Descriptor (`SD`) de uma tarefa no cache do Registro (`HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Schedule\TaskCache\Tree`). 

Sem o `SD`, a API COM nativa do Windows (usada pelo `schtasks.exe`, PowerShell `Get-ScheduledTask` e Autoruns) não consegue enumerar e nem exibir a tarefa, tornando-a "invisível" para a maioria das ferramentas de administração e AVs. No entanto, o serviço interno do agendador (`svchost.exe` - `Schedule`) continua lendo e executando a tarefa silenciosamente.

## A Abordagem do GhostTrace
Para mitigar esse "ponto cego", o GhostTrace emprega um motor de varredura duplo, que coleta artefatos sem modificar o estado do SO (somente-leitura):

1. **ScheduledTasksScanModule (Visão COM)**: Enumera a API operacional viva usando a interface de automação COM (`Schedule.Service`). Representa o que o Windows *acredita* que existe e o que analistas comuns enxergam.
2. **TaskCacheScanModule (Visão Registry)**: Lê diretamente as chaves de controle da árvore `TaskCache\Tree`. Foca exclusivamente na estrutura forense do arquivo de Registro, detectando ausência de `SD` (`MISSING_SD`) ou de índice.

O **ScheduledTasksCorrelationOrchestrator** une esses dois universos e faz o *cross-match* pelo caminho lógico das tarefas, apontando assimetrias.

## Comandos

O comando responsável por executar as coletas e realizar a fusão analítica em um único passo é:

```powershell
GhostTrace.exe scan-tasks-correlate-json --output c:\triage\tasks-correlation.json
```

*Nota: O GhostTrace é rigorosamente read-only. A correlação não conserta, deleta ou bloqueia tarefas detectadas.*

## Interpretação dos Resultados

O arquivo JSON de saída expõe um nó `"correlatedFindings"` classificado com Rótulos (*Labels*) e Severidades.

### 1. GhostCandidate (High)
* **O que significa**: A tarefa existe fisicamente no Registro, mas está oculta no COM e possui a ausência explícita do `SD` (ou corrupção de Índice).
* **Ação Analítica**: Altíssima probabilidade de comprometimento malicioso. Identifique a ação (executável) lendo a chave `Actions` no registro (atualmente manual via Regedit ou EDR).

### 2. StructuralAnomaly (Medium)
* **O que significa**: A tarefa tem corrupções no Registro (ex: `MISSING_SD`), mas por alguma anomalia ou dessincronização o sistema COM *ainda* enxerga e reporta a tarefa.
* **Ação Analítica**: A técnica de evasão do atacante falhou parcialmente, ou o SO está corrompido. Revise a tarefa.

### 3. StructuralOnly (Medium)
* **O que significa**: A tarefa existe no Registro, *não* possui marcadores óbvios de corrupção, mas não foi reportada pela API COM.
* **Ação Analítica**: Pode ser resquício de uma desinstalação defeituosa, dessincronização de cache de SO, ou uma variante de evasão ainda não documentada em que o atacante corrompe algo fora do `SD`. 

### 4. Inconclusive (Info)
* **O que significa**: O GhostTrace falhou em recuperar qualquer tarefa COM, mas obteve tarefas do Registro. 
* **Motivo**: Para evitar um dilúvio de Falsos Positivos, o motor de correlação trava e não assume que todas as tarefas da máquina são fantasmas. Ocorre quando há bloqueios rígidos de permissão, WMI/RPC quebrado ou antivírus bloqueando acesso COM.

## Lendo Metadados e Avisos
O relatório traz propriedades para rápida ingestão em SIEM/Splunk:
* `metadata`: Contém um agrupamento tático de inteligência. Exemplo: `Severity_High: 1`, dispensando scripts complexos de agregação.
* `warnings`: Leia sempre os avisos. Se a varredura encontrou anomalias ambientais, os bloqueios e filtragens parciais estarão relatados em texto claro nesta lista.

## Limitações e Cuidados Forenses (V1)
* **Matching Limitado (Path-based)**: A correlação v1 assume que o nome lógico e as pastas batem perfeitamente. Alterações sutis de case-sensitive pelo atacante podem quebrar o join.
* **Profundidade do Payload**: Por focar na estrutura da ocultação, a V1 sinaliza o caminho comprometido (ex: `\Microsoft\Windows\WinUpdateVbs`), mas **não extrai** o comando exato ou argumentos embutidos no binário ou XML. Essa extração deve ser feita no próximo estágio do IR pelo analista.
* **Acesso Mínimo**: A varredura de `TaskCache` exige privilégios de Administrador, caso contrário falhará ao acessar a chave `Tree`.

---
*GhostTrace Architecture Team*
