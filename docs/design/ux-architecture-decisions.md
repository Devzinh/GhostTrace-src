# UX and Architecture Decision Record: GhostTrace End-User Experience

Este documento consolida as decisões arquiteturais sobre a Distribuição, CLI e TUI do GhostTrace, servindo como contrato para o desenvolvimento das próximas sprints. Nenhuma destas decisões altera a lógica de coleta (Core/Modules).

---

## Decisão 1: Estratégia de Distribuição do Binário

### Opções Consideradas
1. **Múltiplos Arquivos vs PublishSingleFile=true**
2. **Framework-Dependent vs Self-Contained**

### Trade-offs
* **Múltiplos Arquivos**: Facilita o debug e diminui ligeiramente o tamanho do executável principal, mas gera uma pasta poluída com dezenas de DLLs. Para o analista de Resposta a Incidentes (IR), copiar 50 arquivos para um servidor comprometido, calcular hashes de cada um e evitar side-loading de DLLs maliciosas pelo atacante é um pesadelo logístico.
* **Framework-Dependent**: Gera um binário de 1MB, mas exige que o servidor alvo tenha o .NET 8 Runtime instalado. Instalar dependências em uma máquina comprometida viola o princípio forense de menor alteração de estado.
* **Self-Contained + SingleFile**: O binário fica grande (~60MB), mas carrega o runtime inteiro em um arquivo único. Apenas um hash para validar, apenas um arquivo para copiar via EDR/pendrive.

### Decisão Recomendada
* **Formato**: `Self-Contained` com `PublishSingleFile=true`. 
* **Ajuste do `.csproj`**: A alteração **não** deve ser feita agora. Durante o desenvolvimento, compilar em SingleFile atrasa o loop de feedback local (builds mais demorados) e atrapalha o debug passo-a-passo no VS Code. O `.csproj` (ou um script de publish dedicado) será ajustado apenas na sprint de empacotamento final.

  *Validação antecipada obrigatória: realizar um `dotnet publish` de validação após o quinto módulo ser implementado, antes da sprint de UX. Objetivo: detectar incompatibilidades de `PublishSingleFile` (ex: acesso a caminhos relativos de assembly, reflection sobre tipos em DLLs separadas) enquanto o custo de correção ainda é baixo. Essa validação não altera o fluxo de desenvolvimento — é apenas uma checagem pontual.*

### Justificativa
A inviolabilidade da cadeia de custódia forense e a facilidade de *deployment* em máquinas comprometidas isoladas (Air-gapped) tornam o formato estático e autossuficiente a única opção viável para uma ferramenta profissional de IR.

---

## Decisão 2: Comando Agregador `scan --full`

### Opções Consideradas (Discovery)
* **Lista Hard-coded (Registro Explícito)** vs **Reflexão Dinâmica (Assembly Scanning)**

### Trade-offs
* Reflexão dinâmica permite adicionar módulos sem tocar no orquestrador, mas introduz instabilidade forense: a ordem de execução não é garantida, e um atacante poderia teoricamente dropar uma DLL maliciosa com a interface `IScanModule` na pasta da ferramenta para executá-la durante o scan. 
* Lista hard-coded garante que 100% da execução seja previsível, auditável e imutável.

### Decisão Recomendada (Contrato do Agregador)
1. **Discovery**: Hard-coded no `Program.cs` ou numa `ModuleFactory` estática. A ferramenta sabe exatamente quais módulos possui.
2. **Formato do Relatório**: O `scan --full` **não** mesclará tudo num único `IScanResult`. O formato atual do `JsonReportWriter` (que usa uma lista `IReadOnlyList<IScanResult>`) já está perfeito. O arquivo de saída terá um envelope com metadados da varredura global, contendo um array de Resultados Independentes por módulo. Isso preserva timestamps e erros específicos de cada componente.
3. **Falhas Parciais**: O Pipeline executará `Task.WhenAll` (ou sequencial isolado) com blocos `try/catch` ao redor da invocação de cada módulo. Se o `RegistryScanModule` lançar exceção fatal, o orquestrador o marca como `Failed`, anexa a Exception em sua lista de `Errors`, e continua os demais. O console mostrará uma tabela de resumo apontando onde houve sucesso ou falha parcial.

### Justificativa
Forense exige previsibilidade absoluta. O relatório agregado mantendo a segregação por módulo garante que o analista saiba com precisão cirúrgica de onde veio cada finding e qual etapa exata falhou por falta de privilégios.

---

## Decisão 3: TUI Interativa com Spectre.Console

### Opções Consideradas (Entrada)
* Flag explícita (`--tui`) vs Fallback automático.

### Fluxo e Decisão Recomendada
A TUI será ativada **automaticamente se o GhostTrace for executado sem argumentos** (`GhostTrace.exe`). Se *qualquer* argumento for detectado (ex: `--output`, `scan`), a ferramenta entra imediatamente no modo CLI/Headless. Isso descarta a necessidade de uma flag `--no-tui` e facilita a automação via EDR/scripts.

### Storyboard da TUI
1. **Splash Screen & EULA Forense**: Exibe banner ASCII, versão e um aviso crítico em vermelho: *"Esta é uma ferramenta forense de leitura. Você concorda em iniciar a coleta?"* (Y/N).
2. **Seleção de Módulos (Targeting)**: Um componente `MultiSelectionPrompt` do Spectre.Console listando os módulos (Filesystem, Registry, Scheduled Tasks (COM+Reg), etc). O usuário marca/desmarca o que quer rodar usando a barra de espaço.
3. **Configuração de Output**: Pede o caminho do diretório de saída e apresenta um `SelectionPrompt` para o Formato (JSON é o default).
4. **Execução (Live Progress)**: Uso do componente `Progress` do Spectre. Mostra uma barra de progresso geral e sub-barras (Spinners) para os módulos que estão rodando no momento. 
5. **Relatório Final**: Após a conclusão, a tela limpa e desenha uma `Table` de resumo com 4 colunas: *Módulo*, *Status (Success/Partial)*, *Total Findings*, *Warnings/Errors*. Abaixo, o path completo para o relatório gerado.

### Interação com Cancelamento (Ctrl+C)
Na TUI, o `Ctrl+C` **não** pode derrubar o processo abruptamente, pois os relatórios em memória se perderiam. 
* O Spectre.Console será configurado para capturar a interrupção. 
* Ele sinalizará o `CancellationTokenSource` global.
* A UI mudará o status para `[Cancelando cooperativamente...]`.
* A cadeia de responsabilidade do salvamento será:
  - `ScanPipeline`: retorna todos os resultados coletados até o momento do cancelamento, independente do `CancellationToken` ter sido acionado — ele nunca descarta o que já foi lido;
  - Comando agregador (`scan --full`): é o responsável por gravar o arquivo de relatório com os resultados parciais, marcando o status geral como `CancelledPartial` ou equivalente;
  - TUI: apenas exibe o status e o path do arquivo gerado — não toca em I/O de relatório.

*Justificativa: manter a TUI como camada de apresentação pura. Se o salvamento ficar implícito, cada implementador escolherá um lugar diferente, criando comportamento inconsistente entre o modo TUI e o modo CLI headless.*

---

## Impactos no Código Atual e Futuro

1. **Módulos Existentes**: Nenhum módulo (`FilesystemScanModule`, `TaskCacheScanModule`, etc.) sofre impacto de refatoração, pois eles já foram construídos respeitando o `CancellationToken` e retornando `IScanResult`. Eles são agnósticos quanto a quem os invoca (CLI ou TUI).
2. **Pipeline Atual**: O `ScanPipeline` já suporta receber arrays de módulos. Não há quebras de contrato.
3. **Novos Módulos (Futuros)**: Todo novo módulo (Shimcache, Prefetch) deve continuar isolado e não deve injetar `Console.WriteLine` (pois isso quebraria o layout fluido do Spectre.Console). Devem apenas retornar seus Findings pacientemente para o Orquestrador.
4. **Relatórios JSON**: O `JsonReportHelper` já recebe `IReadOnlyList<IScanResult>`, que é o formato exigido pelo `scan --full`. Apenas necessitará de adaptações de UX (supressão de texto no console) ao rodar dentro do modo TUI para não sujar o buffer de tela.

---

## Decisões Pendentes / Riscos Identificados

*Caso não coberto: execução automatizada sem argumentos. A regra atual (presença de argumentos = modo headless) não cobre cenários onde `GhostTrace.exe` é invocado sem argumentos por automação — ex: agendamento via Task Scheduler, hook de EDR, ou script silencioso. Duas opções a decidir antes da sprint de UX: (a) flag explícita `--no-tui`; (b) variável de ambiente `GHOSTTRACE_HEADLESS=1`. Trade-off: a flag é mais explícita e auditável em logs de execução; a variável de ambiente é mais flexível para ambientes onde a linha de comando não pode ser modificada. Decisão deve ser tomada e registrada aqui antes de iniciar a implementação da TUI.*
