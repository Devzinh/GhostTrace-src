using System;
using System.Globalization;

namespace GhostTrace.Core.Localization;

/// <summary>
/// Central access point for localized strings. The active language is resolved once from
/// the OS UI culture (overridable), and exposed via <see cref="Current"/>.
/// </summary>
public static class Loc
{
    private static LocaleStrings _current = new(); // English defaults until InitializeFromOs()

    /// <summary>The active language's strings.</summary>
    public static LocaleStrings Current => _current;

    /// <summary>Two-letter code of the active language ("en", "pt", "es").</summary>
    public static string ActiveLanguage { get; private set; } = "en";

    /// <summary>
    /// Resolves the active language from the OS UI culture. "pt" → Portuguese,
    /// "es" → Spanish, everything else → English.
    /// </summary>
    public static void InitializeFromOs()
    {
        string lang;
        try
        {
            lang = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
        }
        catch
        {
            lang = "en";
        }
        SetLanguage(lang);
    }

    /// <summary>
    /// Forces a specific language by two-letter code or full culture name
    /// (e.g. "pt", "pt-BR", "es-ES"). Unknown values fall back to English.
    /// </summary>
    public static void SetLanguage(string? language)
    {
        string two = (language ?? "en").Trim().ToLowerInvariant();
        if (two.Length > 2) two = two.Substring(0, 2);

        (_current, ActiveLanguage) = two switch
        {
            "pt" => (Portuguese, "pt"),
            "es" => (Spanish, "es"),
            _ => (English, "en"),
        };
    }

    public static readonly LocaleStrings English = new();

    public static readonly LocaleStrings Portuguese = new()
    {
        Tagline = "Caçador de Rastros Forenses",
        BadgeReadOnly = "SOMENTE-LEITURA",
        BadgeOffline = "OFFLINE",
        BadgeNoMutations = "SCAN SÓ-LEITURA",

        MenuMainTitle = "Menu principal",
        MenuChooseAction = "escolha uma ação",
        MenuHuntTitle = "Buscar rastros de software",
        MenuHuntDesc = "Caça um software por todas as técnicas forenses",
        MenuAboutTitle = "Sobre o GhostTrace",
        MenuAboutDesc = "Versão, técnicas, garantias",
        MenuExitTitle = "Sair",
        MenuExitDesc = "Encerra a sessão",
        PromptSoftwareName = "Nome do software",
        PromptSoftwareNameHint = "ex: nvidia, adobe, steam",
        ErrorEmptyName = "Não pode ser vazio.",
        PromptOutputDir = "Diretório de saída",
        EnterEquals = "Enter =",
        PromptAnotherSearch = "Buscar outro software?",
        YesNoExitHint = "S = sim · qualquer outra = sair",
        AffirmativeKey = "S",
        Goodbye = "Encerrando GhostTrace.",

        AboutHeader = "Sobre GhostTrace",
        LblVersion = "Versão",
        LblPlatform = "Plataforma",
        LblModules = "Módulos",
        LblMode = "Modo",
        ModulesValue = "22 forenses",
        ModeValue = "Scan read-only · Limpeza opt-in · Offline",
        ColModule = "Módulo",
        ColCoverage = "Cobertura",
        GuaranteesTitle = "Garantias",
        Guarantee1 = "A varredura é somente leitura — nada é alterado durante o scan.",
        Guarantee2 = "A remoção só ocorre após você selecionar e confirmar (SIM), com log.",
        Guarantee3 = "Evidência forense (execução/histórico) nunca é apagável.",
        Guarantee4 = "Nenhuma chamada de rede é feita.",
        PressAnyKey = "Pressione qualquer tecla para voltar...",

        PanelHeaderHunter = "GhostTrace  v{0}  -  Caçador de Rastros",
        LblHost = "Host",
        LblOs = "SO",
        LblStart = "Início",
        LblTarget = "Alvo",
        LblOutput = "Saída",
        FullTriage = "(triagem completa)",

        SummaryHeader = "Resultado",
        LblTechniques = "Técnicas",
        LblTraces = "Rastros",
        LblStatus = "Status",
        TracesOfFmt = "{0} de {1}",
        NoTracesOfFmt = "nenhum rastro de \"{0}\"",
        MatchesTitleFmt = "Rastros de \"{0}\"",
        ColTechnique = "Técnica",
        ColCategory = "Categoria",
        ColArtifact = "Artefato",
        ColWhen = "Quando",
        NoTracesFoundFmt = "Nenhum rastro de {0} encontrado.",
        MoreTracesFmt = "… e mais {0} rastros no relatório",

        NoRemovable = "Nenhum rastro removível (apenas evidência de execução foi encontrada — não é apagável).",
        PromptRemoveFmt = "Remover rastros de \"{0}\"?",
        RemovableCountFmt = "({0} removíveis)",
        SelectToRemove = "Selecione o que remover",
        SelectHint = "evidência forense não aparece aqui",
        MultiSelectInstr = "(<espaço> marcar · <enter> confirmar)",
        MoreChoices = "(role para ver mais)",
        NothingSelected = "Nada selecionado — nenhuma remoção feita.",
        WarnDeleteFmt = "{0} item(ns) serão apagados permanentemente.",
        Warning = "ATENÇÃO:",
        TypeToConfirmFmt = "Digite {0} para confirmar:",
        ConfirmWord = "SIM",
        Cancelled = "Cancelado — nada foi removido.",
        TagRemoved = "removido",
        TagSkipped = "pulado",
        TagError = "erro",
        CleanupSummaryFmt = "{0} removido(s) · {1} pulado(s) · {2} erro(s)",
        LblLog = "Log",
        PromptExportReport = "Exportar relatório (.txt)?",
        LblReport = "Relatório",
        KindFolder = "Pasta",
        KindFile = "Arquivo",
        KindRegistry = "Registro",
        NotFoundFmt = "não encontrado: {0}",
        InvalidRegPathFmt = "caminho de registro inválido: {0}",
        UnsupportedHiveFmt = "hive não suportada: {0}",
        KeyNotFoundFmt = "chave não encontrada: {0}",
        ValueNotFoundFmt = "valor não encontrado: {0}",

        LogTitle = "GhostTrace - Log de Limpeza",
        LogGenerated = "Gerado",
        LogTarget = "Alvo",
        LogCounts = "Removidos: {0} | Pulados: {1} | Erros: {2}",

        RequiresWindows = "GhostTrace requer Windows.",
        NoModulesSelected = "Nenhum módulo selecionado para executar.",
        OutputDirErrorFmt = "Não foi possível criar o diretório de saída '{0}': {1}",
        ScanCancelled = "Busca cancelada pelo usuário.",
        ReportWriteWarnFmt = "Aviso: falha ao gerar relatório: {0}",

        PrivInsufficient = "Privilégios insuficientes.",
        PrivRequiresAdmin = "GhostTrace precisa ser executado como Administrador.",
        PrivRightClick = "Clique com o botão direito no executável e escolha",
        PrivRunAsAdmin = "\"Executar como administrador\".",
        PrivPressKey = "Pressione qualquer tecla para sair.",

        RptTitle = "GhostTrace - Relatório Forense",
        RptHost = "Host",
        RptOs = "SO",
        RptStarted = "Início",
        RptFinished = "Fim",
        RptDuration = "Duração",
        RptFilter = "Filtro",
        RptFindings = "Achados",
        RptMatches = "Rastros",
        RptStatus = "Status",
        RptModules = "Módulos",
        RptColName = "Nome",
        RptColStatus = "Status",
        RptColFindings = "Achados",
        RptColMatches = "Rastros",
        RptColErrors = "Erros",
        RptColDuration = "Duração",
        RptMatchedFindings = "Rastros encontrados",
        RptDescription = "Descrição",
        RptSource = "Origem",
        RptRawValue = "Valor",
    };

    public static readonly LocaleStrings Spanish = new()
    {
        Tagline = "Cazador de Rastros Forenses",
        BadgeReadOnly = "SOLO-LECTURA",
        BadgeOffline = "SIN CONEXIÓN",
        BadgeNoMutations = "ESCANEO SOLO-LECTURA",

        MenuMainTitle = "Menú principal",
        MenuChooseAction = "elija una acción",
        MenuHuntTitle = "Buscar rastros de software",
        MenuHuntDesc = "Caza un programa por todas las técnicas forenses",
        MenuAboutTitle = "Acerca de GhostTrace",
        MenuAboutDesc = "Versión, técnicas, garantías",
        MenuExitTitle = "Salir",
        MenuExitDesc = "Finaliza la sesión",
        PromptSoftwareName = "Nombre del software",
        PromptSoftwareNameHint = "ej: nvidia, adobe, steam",
        ErrorEmptyName = "No puede estar vacío.",
        PromptOutputDir = "Directorio de salida",
        EnterEquals = "Enter =",
        PromptAnotherSearch = "¿Buscar otro programa?",
        YesNoExitHint = "S = sí · cualquier otra = salir",
        AffirmativeKey = "S",
        Goodbye = "Cerrando GhostTrace.",

        AboutHeader = "Acerca de GhostTrace",
        LblVersion = "Versión",
        LblPlatform = "Plataforma",
        LblModules = "Módulos",
        LblMode = "Modo",
        ModulesValue = "22 forenses",
        ModeValue = "Escaneo solo-lectura · Limpieza opcional · Sin conexión",
        ColModule = "Módulo",
        ColCoverage = "Cobertura",
        GuaranteesTitle = "Garantías",
        Guarantee1 = "El escaneo es solo-lectura — nada se modifica al escanear.",
        Guarantee2 = "La eliminación ocurre solo tras seleccionar y confirmar (SÍ), con registro.",
        Guarantee3 = "La evidencia forense (ejecución/historial) nunca es eliminable.",
        Guarantee4 = "No se realizan llamadas de red.",
        PressAnyKey = "Presione cualquier tecla para volver...",

        PanelHeaderHunter = "GhostTrace  v{0}  -  Cazador de Rastros",
        LblHost = "Host",
        LblOs = "SO",
        LblStart = "Inicio",
        LblTarget = "Objetivo",
        LblOutput = "Salida",
        FullTriage = "(triaje completo)",

        SummaryHeader = "Resultado",
        LblTechniques = "Técnicas",
        LblTraces = "Rastros",
        LblStatus = "Estado",
        TracesOfFmt = "{0} de {1}",
        NoTracesOfFmt = "sin rastros de \"{0}\"",
        MatchesTitleFmt = "Rastros de \"{0}\"",
        ColTechnique = "Técnica",
        ColCategory = "Categoría",
        ColArtifact = "Artefacto",
        ColWhen = "Cuándo",
        NoTracesFoundFmt = "No se encontraron rastros de {0}.",
        MoreTracesFmt = "… y {0} rastros más en el informe",

        NoRemovable = "Sin rastros eliminables (solo se encontró evidencia de ejecución — no eliminable).",
        PromptRemoveFmt = "¿Eliminar rastros de \"{0}\"?",
        RemovableCountFmt = "({0} eliminables)",
        SelectToRemove = "Seleccione qué eliminar",
        SelectHint = "la evidencia forense no aparece aquí",
        MultiSelectInstr = "(<espacio> marcar · <enter> confirmar)",
        MoreChoices = "(desplácese para ver más)",
        NothingSelected = "Nada seleccionado — no se eliminó nada.",
        WarnDeleteFmt = "{0} elemento(s) serán eliminados permanentemente.",
        Warning = "ATENCIÓN:",
        TypeToConfirmFmt = "Escriba {0} para confirmar:",
        ConfirmWord = "SÍ",
        Cancelled = "Cancelado — no se eliminó nada.",
        TagRemoved = "eliminado",
        TagSkipped = "omitido",
        TagError = "error",
        CleanupSummaryFmt = "{0} eliminado(s) · {1} omitido(s) · {2} error(es)",
        LblLog = "Registro",
        PromptExportReport = "¿Exportar informe (.txt)?",
        LblReport = "Informe",
        KindFolder = "Carpeta",
        KindFile = "Archivo",
        KindRegistry = "Registro",
        NotFoundFmt = "no encontrado: {0}",
        InvalidRegPathFmt = "ruta de registro no válida: {0}",
        UnsupportedHiveFmt = "hive no compatible: {0}",
        KeyNotFoundFmt = "clave no encontrada: {0}",
        ValueNotFoundFmt = "valor no encontrado: {0}",

        LogTitle = "GhostTrace - Registro de Limpieza",
        LogGenerated = "Generado",
        LogTarget = "Objetivo",
        LogCounts = "Eliminados: {0} | Omitidos: {1} | Errores: {2}",

        RequiresWindows = "GhostTrace requiere Windows.",
        NoModulesSelected = "Ningún módulo seleccionado para ejecutar.",
        OutputDirErrorFmt = "No se pudo crear el directorio de salida '{0}': {1}",
        ScanCancelled = "Búsqueda cancelada por el usuario.",
        ReportWriteWarnFmt = "Aviso: error al generar el informe: {0}",

        PrivInsufficient = "Privilegios insuficientes.",
        PrivRequiresAdmin = "GhostTrace debe ejecutarse como Administrador.",
        PrivRightClick = "Haga clic derecho en el ejecutable y elija",
        PrivRunAsAdmin = "\"Ejecutar como administrador\".",
        PrivPressKey = "Presione cualquier tecla para salir.",

        RptTitle = "GhostTrace - Informe Forense",
        RptHost = "Host",
        RptOs = "SO",
        RptStarted = "Inicio",
        RptFinished = "Fin",
        RptDuration = "Duración",
        RptFilter = "Filtro",
        RptFindings = "Hallazgos",
        RptMatches = "Rastros",
        RptStatus = "Estado",
        RptModules = "Módulos",
        RptColName = "Nombre",
        RptColStatus = "Estado",
        RptColFindings = "Hallazgos",
        RptColMatches = "Rastros",
        RptColErrors = "Errores",
        RptColDuration = "Duración",
        RptMatchedFindings = "Rastros encontrados",
        RptDescription = "Descripción",
        RptSource = "Origen",
        RptRawValue = "Valor",
    };
}
